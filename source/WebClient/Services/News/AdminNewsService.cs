using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.News;
using WebClient.Services.Shared;

namespace WebClient.Services.News;

public interface IAdminNewsService
{
    Task<NewsApiResponse<PagedResult<AdminNewsListItemDto>>?> GetNewsAsync(AdminNewsQueryDto query);

    Task<NewsApiResponse<List<NewsCategoryDto>>?> GetCategoriesAsync();

    Task<NewsApiResponse<AdminNewsDetailDto>?> GetNewsDetailAsync(Guid id);

    Task<NewsApiResponse<AdminNewsDetailDto>?> CreateNewsAsync(AdminCreateNewsDto dto);

    Task<NewsApiResponse<AdminNewsDetailDto>?> UpdateNewsAsync(Guid id, AdminUpdateNewsDto dto);

    Task<NewsApiResponse<object>?> DeleteNewsAsync(Guid id);

    Task<NewsApiResponse<object>?> RestoreNewsAsync(Guid id);
}

public sealed class AdminNewsService : IAdminNewsService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminNewsService(
        IApiClient apiClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = apiClient.HttpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<NewsApiResponse<PagedResult<AdminNewsListItemDto>>?> GetNewsAsync(AdminNewsQueryDto query)
    {
        if (query.PageIndex <= 0)
            query.PageIndex = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var queryParams = new Dictionary<string, string?>
        {
            ["PageIndex"] = query.PageIndex.ToString(),
            ["PageSize"] = query.PageSize.ToString()
        };

        var hasFilter =
            !string.IsNullOrWhiteSpace(query.Keyword) ||
            !string.IsNullOrWhiteSpace(query.Status) ||
            query.CategoryId.HasValue;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
            queryParams["Keyword"] = query.Keyword.Trim();

        if (!string.IsNullOrWhiteSpace(query.Status))
            queryParams["Status"] = query.Status.Trim();

        if (query.CategoryId.HasValue)
            queryParams["CategoryId"] = query.CategoryId.Value.ToString();

        var path = hasFilter
            ? "api/admin/news/filter"
            : "api/admin/news";

        var url = QueryHelpers.AddQueryString(path, queryParams);

        return await GetAsync<PagedResult<AdminNewsListItemDto>>(url);
    }

    public async Task<NewsApiResponse<List<NewsCategoryDto>>?> GetCategoriesAsync()
    {
        return await GetAsync<List<NewsCategoryDto>>("api/admin/news/categories");
    }

    public async Task<NewsApiResponse<AdminNewsDetailDto>?> GetNewsDetailAsync(Guid id)
    {
        return await GetAsync<AdminNewsDetailDto>($"api/admin/news/{id}");
    }

    public async Task<NewsApiResponse<AdminNewsDetailDto>?> CreateNewsAsync(AdminCreateNewsDto dto)
    {
        return await SendJsonAsync<AdminNewsDetailDto>(
            HttpMethod.Post,
            "api/admin/news",
            dto
        );
    }

    public async Task<NewsApiResponse<AdminNewsDetailDto>?> UpdateNewsAsync(Guid id, AdminUpdateNewsDto dto)
    {
        return await SendJsonAsync<AdminNewsDetailDto>(
            HttpMethod.Put,
            $"api/admin/news/{id}",
            dto
        );
    }

    public async Task<NewsApiResponse<object>?> DeleteNewsAsync(Guid id)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Delete,
            $"api/admin/news/{id}",
            new { }
        );
    }

    public async Task<NewsApiResponse<object>?> RestoreNewsAsync(Guid id)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Post,
            $"api/admin/news/{id}/restore",
            new { }
        );
    }

    private async Task<NewsApiResponse<T>?> GetAsync<T>(string path)
    {
        try
        {
            var requestUrl = path.TrimStart('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            AddBearerToken(request);

            using var response = await _httpClient.SendAsync(request);

            var body = await ReadBodyAsync<T>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return body;
            }

            return new NewsApiResponse<T>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {requestUrl}"
            };
        }
        catch (Exception ex)
        {
            return new NewsApiResponse<T>
            {
                Success = false,
                Message = $"Cannot call News API. Detail: {ex.Message}"
            };
        }
    }

    private async Task<NewsApiResponse<TResponse>?> SendJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        object payload)
    {
        try
        {
            var requestUrl = path.TrimStart('/');

            using var request = new HttpRequestMessage(method, requestUrl);

            AddBearerToken(request);

            if (method != HttpMethod.Delete)
            {
                request.Content = JsonContent.Create(payload);
            }

            using var response = await _httpClient.SendAsync(request);

            if (method == HttpMethod.Delete && response.IsSuccessStatusCode)
            {
                return new NewsApiResponse<TResponse>
                {
                    Success = true,
                    Message = "News article deleted successfully."
                };
            }

            var body = await ReadBodyAsync<TResponse>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return body;
            }

            return new NewsApiResponse<TResponse>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {requestUrl}"
            };
        }
        catch (Exception ex)
        {
            return new NewsApiResponse<TResponse>
            {
                Success = false,
                Message = $"Cannot call News API. Detail: {ex.Message}"
            };
        }
    }

    private static readonly JsonSerializerOptions BodyJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static async Task<NewsApiResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
    {
        string raw;

        try
        {
            raw = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var body = JsonSerializer.Deserialize<NewsApiResponse<T>>(raw, BodyJsonOptions);

            if (body?.Message != null)
            {
                return body;
            }

            // NewsAPI answers in two different shapes. Business failures come back as
            // ApiResponse (handled above), but [ApiController] short-circuits model validation
            // BEFORE the action runs and answers with ValidationProblemDetails, which has no
            // "message" at all. Without this branch every such 400 collapsed into the caller's
            // bare "Request failed HTTP 400" fallback, hiding which field was rejected.
            var problem = ReadProblemDetailsMessage(raw);

            if (problem != null)
            {
                return new NewsApiResponse<T>
                {
                    Success = false,
                    Message = problem
                };
            }

            return body;
        }
        catch
        {
            return null;
        }
    }

    // Flattens ValidationProblemDetails into one sentence: every message under "errors",
    // falling back to "title" when the payload carries no per-field detail.
    private static string? ReadProblemDetailsMessage(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();

                foreach (var field in errors.EnumerateObject())
                {
                    if (field.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var entry in field.Value.EnumerateArray())
                    {
                        var text = entry.GetString();

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            messages.Add(text.Trim());
                        }
                    }
                }

                if (messages.Count > 0)
                {
                    return string.Join(" ", messages);
                }
            }

            // Only treat "title" as an error when "status" is present too, so a success body
            // that happens to carry a title is never mistaken for a problem document.
            if (root.TryGetProperty("status", out _) &&
                root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                var text = title.GetString();

                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void AddBearerToken(HttpRequestMessage request)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext == null)
            return;

        string? token = null;

        if (httpContext.Request.Cookies.TryGetValue("access_token", out var accessToken))
        {
            token = accessToken;
        }
        else if (httpContext.Request.Cookies.TryGetValue("JWToken", out var jwtToken))
        {
            token = jwtToken;
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}