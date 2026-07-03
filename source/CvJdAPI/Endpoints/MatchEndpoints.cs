using System.Text;
using System.Text.Json;
using CvJdAPI.Data;
using CvJdAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CvJdAPI.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        app.MapPost("/match", async (HttpContext ctx, DocumentDbContext db, IHttpClientFactory factory, IConfiguration config) =>
        {
            var body = await JsonSerializer.DeserializeAsync<MatchRequest>(ctx.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (body is null || string.IsNullOrWhiteSpace(body.UserId))
                return Results.BadRequest(new { error = "UserId required" });

            if (!Guid.TryParse(body.UserId, out var userId))
                return Results.BadRequest(new { error = "Invalid UserId" });

            var cv = await db.Cvs.Where(c => c.UserId == userId && c.IsCurrent).OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
            var jd = await db.JobDescriptions.Where(j => j.UserId == userId).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync();

            if (cv?.ParsedJson is null || jd?.ClarifiedJson is null)
                return Results.BadRequest(new { error = "Parse CV and JD first before matching" });

            var aiUrl = config.GetValue<string>("AI_SERVICE_URL") ?? "http://localhost:8000";
            var client = factory.CreateClient();
            var payload = JsonSerializer.Serialize(new
            {
                cv_json = JsonSerializer.Deserialize<object>(cv.ParsedJson),
                jd_json = JsonSerializer.Deserialize<object>(jd.ClarifiedJson),
            });

            var resp = await client.PostAsync($"{aiUrl}/match",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
                return Results.StatusCode(502);

            var resultJson = await resp.Content.ReadAsStringAsync();
            var scoreDoc = JsonSerializer.Deserialize<JsonElement>(resultJson);
            var scoreValue = scoreDoc.TryGetProperty("score", out var scoreProp) ? (decimal)scoreProp.GetInt32() : 0m;

            // Flatten matched/missing keywords to JSON strings for storage
            var matchedKeywords = scoreDoc.TryGetProperty("matched_skills", out var mk) ? mk.GetRawText() : null;
            var missingKeywords = scoreDoc.TryGetProperty("missing_skills", out var ms) ? ms.GetRawText() : null;
            var suggestions = scoreDoc.TryGetProperty("suggestions", out var sg) ? sg.GetRawText() : null;

            db.MatchingScores.Add(new MatchingScore
            {
                UserId = userId,
                CvId = cv.CvId,
                JdId = jd.JdId,
                Score = scoreValue,
                MatchedKeywords = matchedKeywords,
                MissingKeywords = missingKeywords,
                SuggestionText = suggestions,
            });
            await db.SaveChangesAsync();

            return Results.Ok(JsonSerializer.Deserialize<object>(resultJson));
        })
        .WithName("MatchCvJd").WithOpenApi();
    }

    private record MatchRequest(string UserId);
}
