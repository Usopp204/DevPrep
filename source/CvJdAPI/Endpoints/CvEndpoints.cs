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
        app.MapGet("/cv/me", GetMyCvAsync).WithName("GetMyCv").WithOpenApi();

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

            // No literal fallback: the old default was port 8000, which Docker does not publish
            // to the host (it maps 8080:8000), so a missing key failed as "ai-service unreachable"
            // instead of naming the real cause. AI_SERVICE_URL now lives in appsettings.json.
            var aiUrl = config.GetValue<string>("AI_SERVICE_URL")
                ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");
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
            return await UpdateCvJsonAsync(body?.UserId, body?.ParsedJson, db);
        }).WithName("UpdateCvJson").WithOpenApi();

        // DELETE current CV — removes the DB record and the uploaded file.
        app.MapDelete("/cv/me", async (string userId, DocumentDbContext db, IConfiguration config) =>
        {
            if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });

            var cv = await db.Cvs.Where(c => c.UserId == uid && c.IsCurrent)
                .OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
            if (cv is null) return Results.NotFound(new { error = "No CV found" });

            // MatchingScores references Cvs with NO ACTION, so every match this CV took part in has
            // to be gone before the CV row is removed. CvProfiles is ON DELETE CASCADE and clears
            // itself.
            //
            // The two deletes need SEPARATE SaveChanges calls. DocumentDbContext declares no
            // relationship between MatchingScore and Cv - only HasKey - so EF has no dependency
            // graph to order them by, and batching both into one SaveChanges emits the DELETE on
            // Cvs first, which the foreign key rejects. A transaction keeps the pair atomic.
            var matches = await db.MatchingScores.Where(m => m.CvId == cv.CvId).ToListAsync();

            await using var tx = await db.Database.BeginTransactionAsync();

            if (matches.Count > 0)
            {
                db.MatchingScores.RemoveRange(matches);
                await db.SaveChangesAsync();
            }

            db.Cvs.Remove(cv);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            try
            {
                var uploadPath = config.GetValue<string>("CV_UPLOADS_PATH");
                if (string.IsNullOrEmpty(uploadPath))
                    uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "cvs");
                var fileName = Path.GetFileName(cv.FileUrl);
                var fullPath = Path.Combine(uploadPath, fileName);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch { /* file cleanup best-effort — the DB delete already succeeded */ }

            return Results.Ok(new { success = true });
        }).WithName("DeleteMyCv").WithOpenApi();
    }

    public static async Task<IResult> GetMyCvAsync(string? userId, DocumentDbContext db)
    {
        if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });
        var cv = await db.Cvs.Where(c => c.UserId == uid && c.IsCurrent)
            .OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
        return cv is null ? Results.NotFound() : Results.Ok(cv);
    }

    public static async Task<IResult> UpdateCvJsonAsync(string? userId, string? parsedJson, DocumentDbContext db)
    {
        if (!Guid.TryParse(userId, out var uid) || parsedJson is null)
            return Results.BadRequest(new { error = "Invalid request" });

        var cv = await db.Cvs.Where(c => c.UserId == uid && c.IsCurrent)
            .OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
        if (cv is null) return Results.NotFound(new { error = "No CV found" });

        cv.ParsedJson = parsedJson;
        cv.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Results.Ok(new { success = true });
    }

    public static async Task<IResult> DeleteMyCvAsync(string? userId, DocumentDbContext db, IConfiguration config)
    {
        if (!Guid.TryParse(userId, out var uid)) return Results.BadRequest(new { error = "Invalid userId" });
        var cv = await db.Cvs.Where(c => c.UserId == uid && c.IsCurrent).OrderByDescending(c => c.UploadedAt).FirstOrDefaultAsync();
        if (cv is null) return Results.NotFound(new { error = "No CV found" });
        var matches = await db.MatchingScores.Where(m => m.CvId == cv.CvId).ToListAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (matches.Count > 0) { db.MatchingScores.RemoveRange(matches); await db.SaveChangesAsync(); }
        db.Cvs.Remove(cv); await db.SaveChangesAsync(); await tx.CommitAsync();
        try
        {
            var uploadPath = config.GetValue<string>("CV_UPLOADS_PATH");
            if (string.IsNullOrEmpty(uploadPath)) uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "cvs");
            var fullPath = Path.Combine(uploadPath, Path.GetFileName(cv.FileUrl));
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch { }
        return Results.Ok(new { success = true });
    }

    public static async Task<IResult> UploadCvAsync(
        IFormFile? file, string? userIdValue, DocumentDbContext db,
        IHttpClientFactory factory, IConfiguration config)
    {
        if (file is null) return Results.BadRequest(new { error = "No file uploaded" });
        if (!Guid.TryParse(userIdValue, out var userId)) return Results.BadRequest(new { error = "Invalid userId" });
        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (ext is not (".pdf" or ".doc" or ".docx")) return Results.BadRequest(new { error = "Only PDF/DOC/DOCX allowed" });

        var startedAt = DateTime.UtcNow;
        var uploadPath = config.GetValue<string>("CV_UPLOADS_PATH");
        if (string.IsNullOrEmpty(uploadPath)) uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "cvs");
        Directory.CreateDirectory(uploadPath);
        var savedName = $"{userId:N}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        var savedPath = Path.Combine(uploadPath, savedName);
        await using (var stream = File.Create(savedPath)) await file.CopyToAsync(stream);

        var aiUrl = config.GetValue<string>("AI_SERVICE_URL")
            ?? throw new InvalidOperationException("AI_SERVICE_URL is not configured.");
        string? parsedJson = null;
        try
        {
            var client = factory.CreateClient("ai");
            using var form = new MultipartFormDataContent();
            await using var fs = File.OpenRead(savedPath);
            var content = new StreamContent(fs);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(content, "file", file.FileName ?? savedName);
            var response = await client.PostAsync($"{aiUrl}/parse/cv", form);
            if (response.IsSuccessStatusCode) parsedJson = await response.Content.ReadAsStringAsync();
        }
        catch { }

        var hasNewer = await db.Cvs.AnyAsync(c => c.UserId == userId && c.IsCurrent && c.UploadedAt > startedAt);
        if (!hasNewer)
        {
            var previous = await db.Cvs.Where(c => c.UserId == userId && c.IsCurrent).ToListAsync();
            foreach (var item in previous) item.IsCurrent = false;
        }
        db.Cvs.Add(new Cv
        {
            CvId = Guid.NewGuid(), UserId = userId, FileName = file.FileName ?? savedName,
            FileUrl = $"/uploads/cvs/{savedName}", FileType = ext.TrimStart('.'), FileSizeKb = (int)(file.Length / 1024),
            ParsedJson = parsedJson, ParseStatus = parsedJson is null ? "Failed" : "Parsed", UploadedAt = startedAt, IsCurrent = !hasNewer,
        });
        await db.SaveChangesAsync();
        return Results.Ok(new { parsedJson });
    }

    private record CvJsonUpdateRequest(string UserId, string ParsedJson);
}
