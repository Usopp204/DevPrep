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
    Task<string?> GetJdInfoAsync(string userId, string accessToken);
    Task<string?> SaveJdAsync(string userId, string jdText, string? position, string parseMode, string accessToken);
    Task<string?> UploadJdFileAsync(string userId, IFormFile file, string? position, string accessToken);
    Task<string?> AnalyzeAsync(string userId, string accessToken);
}

public sealed class CvJdService(IApiClient apiClient) : ICvJdService
{
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch { return null; }
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { error = ex.Message }); }
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { error = ex.Message }); }
    }

    public async Task<string?> GetJdInfoAsync(string userId, string accessToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/document/jd/me?userId={Uri.EscapeDataString(userId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsStringAsync() : null;
        }
        catch { return null; }
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { error = ex.Message }); }
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { error = ex.Message }); }
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
            using var resp = await apiClient.HttpClient.SendAsync(req);
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return JsonSerializer.Serialize(new { error = ex.Message }); }
    }
}
