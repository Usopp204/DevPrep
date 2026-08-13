using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WebClient.Models.CvJd;
using WebClient.Services.Shared;

namespace WebClient.Services.CvJd;

public interface ICvJdService
{
    CvJdViewModel GetOverview();
    Task<string?> GetCvInfoAsync(string userId, string accessToken);
    Task<string?> UploadCvAsync(string userId, IFormFile file, string accessToken);
    Task<string?> UpdateCvJsonAsync(string userId, string parsedJson, string accessToken);
    Task<string?> DeleteCvAsync(string userId, string accessToken);
    Task<string?> GetJdInfoAsync(string userId, string accessToken);
    Task<string?> SaveJdAsync(string userId, string jdText, string? position, string parseMode, string accessToken);
    Task<string?> UploadJdFileAsync(string userId, IFormFile file, string? position, string accessToken);
    Task<string?> DeleteJdAsync(string userId, string accessToken);
    Task<string?> AnalyzeAsync(string userId, string accessToken);
}

public sealed class CvJdService(IHttpClientFactory httpClientFactory, ILogger<CvJdService> logger) : ICvJdService
{
    // CV/JD traffic uses the "documents" client (335s), not the default 95s one: parsing a CV
    // or JD runs an AI pass that CvJdAPI allows 300s for. See ServiceCollectionExtensions.
    private HttpClient DocumentsClient() =>
        httpClientFactory.CreateClient(ServiceCollectionExtensions.DocumentsHttpClient);

    // Turns a transport failure into the { error } shape the CV/JD page already renders —
    // Views/Profile/CvJd.cshtml:438 does `'Error: ' + data.error`, so the text must read as a
    // sentence after that prefix and must not start with "Error:" itself.
    //
    // The point is to stop framework text reaching the browser: the old code returned
    // ex.Message directly, which on a timeout reads "The request was canceled due to the
    // configured HttpClient.Timeout of 360 seconds elapsing" — meaningless to a candidate and
    // a description of our internals. The exception is logged instead, so debugging keeps it.
    private string FailureJson(Exception ex, string operation)
    {
        // A timeout is worth separating: it is the one failure the user can act on by
        // retrying or using a smaller file, whereas the rest are ours to fix.
        var timedOut = ex is OperationCanceledException;

        if (timedOut)
        {
            logger.LogWarning(ex, "{Operation} timed out.", operation);
        }
        else
        {
            logger.LogError(ex, "{Operation} failed.", operation);
        }

        return JsonSerializer.Serialize(new
        {
            error = timedOut
                ? "Document processing is taking longer than expected. Please try again, or upload a smaller file."
                : "Document processing is temporarily unavailable. Please try again."
        });
    }

    public CvJdViewModel GetOverview() => new()
    {
        ModuleName = "CV & JD Processing",
        Epic = "CV & JD Processing",
        Summary = "CV and JD processing via API Gateway."
    };

    public async Task<string?> GetCvInfoAsync(string userId, string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/document/cv/me?userId={Uri.EscapeDataString(userId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex)
        {
            // C-04 — deliberately still returns null, which the controller turns into a 404 and
            // the page reads as "no CV on file". A failed call is therefore shown to the user as
            // absence of data, which is a lie. Logging at least makes it visible server-side;
            // telling the two apart needs a return type that can carry "failed" separately from
            // "none", and that changes the controller contract. See code-followups.md C-04.
            logger.LogWarning(ex, "Fetching CV info failed; the page will show it as 'no CV on file'.");
            return null;
        }
    }

    public async Task<string?> UploadCvAsync(string userId, IFormFile file, string accessToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(userId), "userId");
            await using var stream = file.OpenReadStream();
            var sc = new StreamContent(stream);
            sc.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(sc, "file", file.FileName ?? "cv");

            using var req = new HttpRequestMessage(HttpMethod.Post, "api/document/cv") { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Uploading a CV"); }
    }

    public async Task<string?> UpdateCvJsonAsync(string userId, string parsedJson, string accessToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, ParsedJson = parsedJson });
            using var req = new HttpRequestMessage(HttpMethod.Put, "api/document/cv/json")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Saving CV edits"); }
    }

    public async Task<string?> DeleteCvAsync(string userId, string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/document/cv/me?userId={Uri.EscapeDataString(userId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Deleting the CV"); }
    }

    public async Task<string?> GetJdInfoAsync(string userId, string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/document/jd/me?userId={Uri.EscapeDataString(userId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex)
        {
            // C-04 — same trade-off as GetCvInfoAsync above: null is indistinguishable from
            // "no JD saved" once it reaches the page. See code-followups.md C-04.
            logger.LogWarning(ex, "Fetching JD info failed; the page will show it as 'no JD on file'.");
            return null;
        }
    }

    public async Task<string?> SaveJdAsync(string userId, string jdText, string? position, string parseMode, string accessToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId, Text = jdText, Position = position, ParseMode = parseMode });
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/document/jd")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Saving a JD"); }
    }

    public async Task<string?> UploadJdFileAsync(string userId, IFormFile file, string? position, string accessToken)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(userId), "userId");
            if (!string.IsNullOrEmpty(position)) form.Add(new StringContent(position), "position");
            await using var stream = file.OpenReadStream();
            var sc = new StreamContent(stream);
            sc.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType);
            form.Add(sc, "file", file.FileName ?? "jd");

            using var req = new HttpRequestMessage(HttpMethod.Post, "api/document/jd/file") { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Uploading a JD file"); }
    }

    public async Task<string?> DeleteJdAsync(string userId, string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/document/jd/me?userId={Uri.EscapeDataString(userId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Deleting the JD"); }
    }

    public async Task<string?> AnalyzeAsync(string userId, string accessToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { UserId = userId });
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/document/match")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await DocumentsClient().SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return FailureJson(ex, "Analyzing the CV against the JD"); }
    }
}
