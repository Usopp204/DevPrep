using System.Text.Json;
using CvJdAPI.Data;
using CvJdAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CvJdAPI.Endpoints;

public static class CvEndpoints
{
    public static void MapCvEndpoints(this WebApplication app)
    {
        // GET current CV info
        app.MapGet("/cv/me", async (string userId, DocumentDbContext db) =>
        {
            if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });
            var cv = await db.Cvs.Where(c => c.UserId == uid && c.IsCurrent)
                .OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
            return cv is null ? Results.NotFound() : Results.Ok(cv);
        }).WithName("GetMyCv").WithOpenApi();

        // POST upload + parse CV file
        app.MapPost("/cv", async (HttpRequest request, DocumentDbContext db, IHttpClientFactory factory, IConfiguration config) =>
        {
            if (!request.Form.Files.Any())
                return Results.BadRequest(new { error = "No file uploaded" });

            var userIdStr = request.Form["userId"].ToString();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.BadRequest(new { error = "Invalid userId" });

            var startedAt = DateTime.UtcNow;

            var file = request.Form.Files[0];
            var ext = Path.GetExtension(file.FileName ?? "").ToLower();
            if (ext is not (".pdf" or ".doc" or ".docx"))
                return Results.BadRequest(new { error = "Only PDF/DOC/DOCX allowed" });

            var uploadPath = config.GetValue<string>("CV_UPLOADS_PATH");
            if (string.IsNullOrEmpty(uploadPath))
                uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "cvs");
            Directory.CreateDirectory(uploadPath);

            var savedName = $"{userId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var savedPath = Path.Combine(uploadPath, savedName);
            await using (var stream = File.Create(savedPath))
                await file.CopyToAsync(stream);

            var aiUrl = config.GetValue<string>("AI_SERVICE_URL") ?? "http://localhost:8000";
            string? parsedJson = null;
            try
            {
                var client = factory.CreateClient("ai");
                using var form = new MultipartFormDataContent();
                await using var fs = File.OpenRead(savedPath);
                var sc = new StreamContent(fs);
                sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
                form.Add(sc, "file", file.FileName ?? savedName);
                var resp = await client.PostAsync($"{aiUrl}/parse/cv", form);
                var body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode)
                    parsedJson = body;
                else
                    Console.Error.WriteLine($"[CV] AI service returned {(int)resp.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CV] AI service call failed: {ex.GetType().Name}: {ex.Message}");
            }

            var hasNewer = await db.Cvs.AnyAsync(c => c.UserId == userId && c.IsCurrent && c.UploadedAt > startedAt);
            if (!hasNewer)
            {
                var prevCvs = await db.Cvs.Where(c => c.UserId == userId && c.IsCurrent).ToListAsync();
                foreach (var prev in prevCvs) prev.IsCurrent = false;
            }

            db.Cvs.Add(new Cv
            {
                UserId = userId,
                FileName = file.FileName ?? savedName,
                FileUrl = $"/uploads/cvs/{savedName}",
                FileType = ext.TrimStart('.'),
                FileSizeKb = (int)(file.Length / 1024),
                ParsedJson = parsedJson,
                ParseStatus = parsedJson is not null ? "Parsed" : "Failed",
                UploadedAt = startedAt,
                IsCurrent = !hasNewer,
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { parsedJson });
        }).WithName("UploadCv").WithOpenApi().DisableAntiforgery();

        // PUT update ParsedJson after user edits
        app.MapPut("/cv/json", async (HttpContext ctx, DocumentDbContext db) =>
        {
            var body = await JsonSerializer.DeserializeAsync<CvJsonUpdateRequest>(ctx.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body is null || !Guid.TryParse(body.UserId, out var userId))
                return Results.BadRequest(new { error = "Invalid request" });

            var cv = await db.Cvs.Where(c => c.UserId == userId && c.IsCurrent)
                .OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
            if (cv is null) return Results.NotFound(new { error = "No CV found" });

            cv.ParsedJson = body.ParsedJson;
            cv.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true });
        }).WithName("UpdateCvJson").WithOpenApi();
    }

    private record CvJsonUpdateRequest(string UserId, string ParsedJson);
}
