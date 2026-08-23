using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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


    Task<QuestionApiResponse<List<AdminPositionDto>>> GetPositionsAsync();
}

public sealed class AdminQuestionService : IAdminQuestionService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminQuestionService(
        IApiClient apiClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = apiClient.HttpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<QuestionApiResponse<AdminPagedResultDto<AdminQuestionListItemDto>>> GetQuestionsAsync(
    AdminQuestionQueryDto query)
    {
        if (query.PageIndex <= 0)
            query.PageIndex = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var queryParams = new Dictionary<string, string?>
        {
            ["Keyword"] = query.Keyword,
            ["Difficulty"] = query.Difficulty,
            // ["TechStack"] = query.TechStack,
            ["Status"] = query.Status,
            ["PageIndex"] = query.PageIndex.ToString(),
            ["PageSize"] = query.PageSize.ToString()
        };

        if (query.CategoryId.HasValue)
        {
            queryParams["CategoryId"] = query.CategoryId.Value.ToString();
        }

        var url = QueryHelpers.AddQueryString(
            "api/admin/questions",
            queryParams
        );

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
            var requestUrl = path.TrimStart('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

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
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {requestUrl}"
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
            var requestUrl = path.TrimStart('/');

            using var request = new HttpRequestMessage(method, requestUrl);

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
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}: {requestUrl}"
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

    private static readonly JsonSerializerOptions BodyJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static async Task<QuestionApiResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
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
            var body = JsonSerializer.Deserialize<QuestionApiResponse<T>>(raw, BodyJsonOptions);

            if (body?.Message != null)
            {
                return body;
            }

            // QuestionAPI answers in two different shapes. Business failures come back as
            // ApiResponse (handled above), but [ApiController] short-circuits model validation
            // BEFORE the action runs and answers with ValidationProblemDetails, which has no
            // "message" at all. Without this branch every such 400 collapsed into the caller's
            // bare "Request failed HTTP 400" fallback, hiding which field was rejected.
            var problem = ReadProblemDetailsMessage(raw);

            if (problem != null)
            {
                return new QuestionApiResponse<T>
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

    public async Task<QuestionApiResponse<List<AdminPositionDto>>> GetPositionsAsync()
    {
        return await GetAsync<List<AdminPositionDto>>("api/admin/questions/positions");
    }
}