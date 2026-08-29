using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CvJdAPI.Data;
using CvJdAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CvJdAPI.Endpoints;

public static class JdEndpoints
{
    public static void MapJdEndpoints(this WebApplication app)
    {
        // GET latest JD
        app.MapGet("/jd/me", GetMyJdAsync).WithName("GetMyJd").WithOpenApi();

        // POST save JD from text — supports AI parse or Normal mode
        app.MapPost("/jd", async (HttpContext ctx, DocumentDbContext db, IHttpClientFactory factory, IConfiguration config) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JdSaveRequest>(ctx.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body is null || string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.Text))
                return Results.BadRequest(new { error = "UserId and Text required" });
            if (!Guid.TryParse(body.UserId, out var userId))
                return Results.BadRequest(new { error = "Invalid UserId" });

            var startedAt = DateTime.UtcNow;

            string? clarifiedJson = null;
            if (body.ParseMode != "normal")
            {
                // No literal fallback: the old default was port 8000, which Docker does not publish
                // to the host (it maps 8080:8000). AI_SERVICE_URL now lives in appsettings.json.
                var aiUrl = config.GetValue<string>("AI_SERVICE_URL")
                    ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");
                try
                {
                    var client = factory.CreateClient("ai");
                    var payload = JsonSerializer.Serialize(new { text = body.Text });
                    var resp = await client.PostAsync($"{aiUrl}/parse/jd",
                        new StringContent(payload, Encoding.UTF8, "application/json"));
                    if (resp.IsSuccessStatusCode) clarifiedJson = await resp.Content.ReadAsStringAsync();
                }
                catch { }
            }
            else
            {
                // Normal mode: wrap raw text as minimal JSON
                clarifiedJson = JsonSerializer.Serialize(new
                {
                    position = body.Position ?? "Unknown",
                    description = body.Text,
                    required_skills = Array.Empty<string>(),
                    nice_to_have = Array.Empty<string>(),
                    mode = "normal"
                });
            }

            var jd = new JobDescription
            {
                UserId = userId,
                Title = body.Position ?? "Job Description",
                Position = body.Position,
                RawText = body.Text,
                ClarifiedJson = clarifiedJson,
                CreatedAt = startedAt,
            };
            db.JobDescriptions.Add(jd);
            await db.SaveChangesAsync();
            return Results.Ok(new { clarifiedJson });
        }).WithName("SaveJd").WithOpenApi();

        // POST upload JD file — always AI parses
        app.MapPost("/jd/file", async (HttpRequest request, DocumentDbContext db, IHttpClientFactory factory, IConfiguration config) =>
        {
            if (!request.Form.Files.Any())
                return Results.BadRequest(new { error = "No file uploaded" });
            var userIdStr = request.Form["userId"].ToString();
            var position = request.Form["position"].ToString();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.BadRequest(new { error = "Invalid userId" });

            var startedAt = DateTime.UtcNow;

            var file = request.Form.Files[0];
            var ext = Path.GetExtension(file.FileName ?? "").ToLower();
            if (ext is not (".pdf" or ".doc" or ".docx"))
                return Results.BadRequest(new { error = "Only PDF/DOC/DOCX allowed" });

            var tmp = Path.GetTempFileName() + ext;
            await using (var fs = File.Create(tmp)) await file.CopyToAsync(fs);

            // No literal fallback: the old default was port 8000, which Docker does not publish
            // to the host (it maps 8080:8000). AI_SERVICE_URL now lives in appsettings.json.
            var aiUrl = config.GetValue<string>("AI_SERVICE_URL")
                ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");
            string? clarifiedJson = null;
            string rawText = string.Empty;
            try
            {
                var client = factory.CreateClient("ai");
                using var form = new MultipartFormDataContent();
                await using var fstream = File.OpenRead(tmp);
                var sc = new StreamContent(fstream);
                sc.Headers.ContentType = new MediaTypeHeaderValue(
                    string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
                form.Add(sc, "file", file.FileName ?? "jd" + ext);
                if (!string.IsNullOrEmpty(position))
                    form.Add(new StringContent(position), "position");
                var resp = await client.PostAsync($"{aiUrl}/parse/jd-file", form);
                if (resp.IsSuccessStatusCode) clarifiedJson = await resp.Content.ReadAsStringAsync();
            }
            catch { }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }

            var jd = new JobDescription
            {
                UserId = userId,
                Title = position ?? "Job Description",
                Position = position,
                RawText = rawText,
                ClarifiedJson = clarifiedJson,
                CreatedAt = startedAt,
            };
            db.JobDescriptions.Add(jd);
            await db.SaveChangesAsync();
            return Results.Ok(new { clarifiedJson });
        }).WithName("UploadJdFile").WithOpenApi().DisableAntiforgery();

        app.MapPut("/jd", async (HttpContext ctx, DocumentDbContext db) =>
        {
            var body = await JsonSerializer.DeserializeAsync<JdUpdateRequest>(ctx.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return await UpdateJdAsync(body?.UserId, body?.Text, body?.Position, db);
        }).WithName("UpdateJd").WithOpenApi();

        // DELETE current JD
        app.MapDelete("/jd/me", async (string userId, DocumentDbContext db) =>
        {
            if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });

            var jd = await db.JobDescriptions.Where(j => j.UserId == uid)
                .OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync();
            if (jd is null) return Results.NotFound(new { error = "No JD found" });

            // Same reason as DELETE /cv/me: MatchingScores references JobDescriptions with NO ACTION,
            // and DocumentDbContext declares no relationship between the two, so EF cannot order the
            // deletes on its own. Each delete gets its own SaveChanges, wrapped in one transaction.
            var matches = await db.MatchingScores.Where(m => m.JdId == jd.JdId).ToListAsync();

            await using var tx = await db.Database.BeginTransactionAsync();

            if (matches.Count > 0)
            {
                db.MatchingScores.RemoveRange(matches);
                await db.SaveChangesAsync();
            }

            db.JobDescriptions.Remove(jd);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { success = true });
        }).WithName("DeleteMyJd").WithOpenApi();
    }

    public static async Task<IResult> GetMyJdAsync(string? userId, DocumentDbContext db)
    {
        if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });
        var jd = await db.JobDescriptions.Where(j => j.UserId == uid)
            .OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync();
        return jd is null ? Results.NotFound() : Results.Ok(jd);
    }

    public static async Task<IResult> DeleteMyJdAsync(string? userId, DocumentDbContext db)
    {
        if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });
        var jd = await db.JobDescriptions.Where(j => j.UserId == uid).OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync();
        if (jd is null) return Results.NotFound(new { error = "No JD found" });
        var matches = await db.MatchingScores.Where(m => m.JdId == jd.JdId).ToListAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (matches.Count > 0) { db.MatchingScores.RemoveRange(matches); await db.SaveChangesAsync(); }
        db.JobDescriptions.Remove(jd); await db.SaveChangesAsync(); await tx.CommitAsync();
        return Results.Ok(new { success = true });
    }

    public static async Task<IResult> SaveJdAsync(
        string? userIdValue, string? text, string? position, string? parseMode,
        DocumentDbContext db, IHttpClientFactory factory, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(userIdValue) || string.IsNullOrWhiteSpace(text))
            return Results.BadRequest(new { error = "UserId and Text required" });
        if (!Guid.TryParse(userIdValue, out var userId))
            return Results.BadRequest(new { error = "Invalid UserId" });

        string? clarifiedJson = null;
        if (parseMode != "normal")
        {
            var aiUrl = config.GetValue<string>("AI_SERVICE_URL")
                ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");
            try
            {
                var payload = JsonSerializer.Serialize(new { text });
                var resp = await factory.CreateClient("ai").PostAsync($"{aiUrl}/parse/jd", new StringContent(payload, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode) clarifiedJson = await resp.Content.ReadAsStringAsync();
            }
            catch { }
        }
        else
        {
            clarifiedJson = JsonSerializer.Serialize(new { position = position ?? "Unknown", description = text, required_skills = Array.Empty<string>(), nice_to_have = Array.Empty<string>(), mode = "normal" });
        }

        db.JobDescriptions.Add(new JobDescription
        {
            JdId = Guid.NewGuid(), UserId = userId, Title = position ?? "Job Description", Position = position,
            RawText = text, ClarifiedJson = clarifiedJson, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return Results.Ok(new { clarifiedJson });
    }

    public static async Task<IResult> UpdateJdAsync(string? userIdValue, string? text, string? position, DocumentDbContext db)
    {
        if (string.IsNullOrWhiteSpace(userIdValue) || string.IsNullOrWhiteSpace(text))
            return Results.BadRequest(new { error = "UserId and Text required" });
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.BadRequest(new { error = "Invalid UserId" });
        var jd = await db.JobDescriptions.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (jd is null) return Results.NotFound(new { error = "No JD found" });
        jd.RawText = text;
        jd.Position = position;
        jd.Title = position ?? "Job Description";
        jd.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    private record JdSaveRequest(string UserId, string Text, string? Position, string? ParseMode);
    private record JdUpdateRequest(string UserId, string Text, string? Position);
}
