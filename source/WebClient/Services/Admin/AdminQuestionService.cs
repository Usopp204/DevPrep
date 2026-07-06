using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Admin;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public interface IAdminQuestionService
{
    Task<QuestionApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>> GetQuestionsAsync(AdminQuestionQueryDto query);

    Task<QuestionApiResponse<List<AdminCategoryDto>>> GetCategoriesAsync();

    Task<AdminQuestionDetailDto?> GetQuestionDetailAsync(Guid id);

    Task<QuestionApiResponse<AdminQuestionDetailDto>> CreateQuestionAsync(AdminCreateQuestionDto dto);

    Task<QuestionApiResponse<AdminQuestionDetailDto>> UpdateQuestionAsync(Guid id, AdminUpdateQuestionDto dto);

    Task<QuestionApiResponse<object>> DeleteQuestionAsync(Guid id);

    Task<QuestionApiResponse<object>> RestoreQuestionAsync(Guid id);

    Task<QuestionApiResponse<object>> BulkDeleteQuestionsAsync(List<Guid> ids);
}

public sealed class AdminQuestionService : IAdminQuestionService
{
    private const string GatewayBaseUrl = "http://localhost:7000/";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminQuestionService(
        IApiClient apiClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = apiClient.HttpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<QuestionApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>> GetQuestionsAsync(AdminQuestionQueryDto query)
    {
        if (query.PageIndex <= 0)
            query.PageIndex = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var queryParams = new Dictionary<string, string?>
        {
            ["Keyword"] = query.Keyword,
            ["QuestionType"] = query.QuestionType,
            ["Level"] = query.Level,
            ["Difficulty"] = query.Difficulty,
            ["TechStack"] = query.TechStack,
            ["Status"] = query.Status,
            ["PageIndex"] = query.PageIndex.ToString(),
            ["PageSize"] = query.PageSize.ToString()
        };

        if (query.CategoryId.HasValue)
        {
            queryParams["CategoryId"] = query.CategoryId.Value.ToString();
        }

        var url = QueryHelpers.AddQueryString("api/admin/questions", queryParams);

        return await GetAsync<AdminPagedResultDto<AdminQuestionListItemDto>>(url);
    }

    public async Task<QuestionApiResponse<List<AdminCategoryDto>>> GetCategoriesAsync()
    {
        return await GetAsync<List<AdminCategoryDto>>("api/admin/questions/categories");
    }

    public async Task<AdminQuestionDetailDto?> GetQuestionDetailAsync(Guid id)
    {
        var response = await GetAsync<AdminQuestionDetailDto>($"api/admin/questions/{id}");

        if (response.Success && response.Data != null)
        {
            return response.Data;
        }

        return null;
    }

    public async Task<QuestionApiResponse<AdminQuestionDetailDto>> CreateQuestionAsync(AdminCreateQuestionDto dto)
    {
        return await SendJsonAsync<AdminQuestionDetailDto>(
            HttpMethod.Post,
            "api/admin/questions",
            dto
        );
    }

    public async Task<QuestionApiResponse<AdminQuestionDetailDto>> UpdateQuestionAsync(Guid id, AdminUpdateQuestionDto dto)
    {
        return await SendJsonAsync<AdminQuestionDetailDto>(
            HttpMethod.Put,
            $"api/admin/questions/{id}",
            dto
        );
    }

    public async Task<QuestionApiResponse<object>> DeleteQuestionAsync(Guid id)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Delete,
            $"api/admin/questions/{id}",
            new { }
        );
    }

    public async Task<QuestionApiResponse<object>> RestoreQuestionAsync(Guid id)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Post,
            $"api/admin/questions/{id}/restore",
            new { }
        );
    }

    public async Task<QuestionApiResponse<object>> BulkDeleteQuestionsAsync(List<Guid> ids)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Post,
            "api/admin/questions/bulk-delete",
            ids
        );
    }

    private async Task<QuestionApiResponse<T>> GetAsync<T>(string path)
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

            return new QuestionApiResponse<T>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {absoluteUrl}"
            };
        }
        catch (Exception ex)
        {
            return new QuestionApiResponse<T>
            {
                Success = false,
                Message = $"Cannot call Question API. Detail: {ex.Message}"
            };
        }
    }

    private async Task<QuestionApiResponse<TResponse>> SendJsonAsync<TResponse>(
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

            var body = await ReadBodyAsync<TResponse>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return body;
            }

            return new QuestionApiResponse<TResponse>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {absoluteUrl}"
            };
        }
        catch (Exception ex)
        {
            return new QuestionApiResponse<TResponse>
            {
                Success = false,
                Message = $"Cannot call Question API. Detail: {ex.Message}"
            };
        }
    }

    private static async Task<QuestionApiResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<QuestionApiResponse<T>>();
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