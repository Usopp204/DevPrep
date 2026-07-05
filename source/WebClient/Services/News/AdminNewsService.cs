using System.Net.Http.Headers;
using System.Net.Http.Json;
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
    private const string GatewayBaseUrl = "http://localhost:7000/";

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
            var absoluteUrl = GatewayBaseUrl + path.TrimStart('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, absoluteUrl);

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
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {absoluteUrl}"
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
            var absoluteUrl = GatewayBaseUrl + path.TrimStart('/');

            using var request = new HttpRequestMessage(method, absoluteUrl);

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
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {absoluteUrl}"
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

    private static async Task<NewsApiResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<NewsApiResponse<T>>();
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