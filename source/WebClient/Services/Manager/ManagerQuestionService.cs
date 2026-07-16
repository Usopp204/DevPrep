using WebClient.Models.Manager;
using WebClient.Models.Shared;
using WebClient.Services.Shared;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Json;

namespace WebClient.Services.Manager;

public class ManagerQuestionService : IManagerQuestionService
{
    private readonly IApiClient _apiClient;

    public ManagerQuestionService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<PagedResult<QuestionBankListItemViewModel>> GetListAsync(int page = 1, int pageSize = 10, string? keyword = null, string? questionType = null, string? level = null, string? difficulty = null, string? status = null, Guid? categoryId = null)
    {
        var url = "/api/admin/manager/questions";
        var queryParams = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrEmpty(keyword)) queryParams["keyword"] = keyword;
        if (!string.IsNullOrEmpty(questionType)) queryParams["questionType"] = questionType;
        if (!string.IsNullOrEmpty(level)) queryParams["level"] = level;
        if (!string.IsNullOrEmpty(difficulty)) queryParams["difficulty"] = difficulty;
        if (!string.IsNullOrEmpty(status)) queryParams["status"] = status;
        if (categoryId.HasValue) queryParams["categoryId"] = categoryId.Value.ToString();

        var requestUrl = QueryHelpers.AddQueryString(url, queryParams);

        try
        {
            var httpResponse = await _apiClient.HttpClient.GetAsync(requestUrl);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return new PagedResult<QuestionBankListItemViewModel>
                {
                    Page = page,
                    PageSize = pageSize
                };
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<QuestionBankListItemViewModel>>>();
            return response?.Data ?? new PagedResult<QuestionBankListItemViewModel>
            {
                Page = page,
                PageSize = pageSize
            };
        }
        catch (HttpRequestException)
        {
            return new PagedResult<QuestionBankListItemViewModel>
            {
                Page = page,
                PageSize = pageSize
            };
        }
    }

    public async Task<QuestionBankDetailViewModel?> GetByIdAsync(Guid id)
    {
        try
        {
            var httpResponse = await _apiClient.HttpClient.GetAsync($"/api/admin/manager/questions/{id}");
            if (!httpResponse.IsSuccessStatusCode)
            {
                return null;
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ApiResponse<QuestionBankDetailViewModel>>();
            return response?.Data;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<QuestionBankCategoryViewModel>> GetCategoriesAsync()
    {
        try
        {
            var httpResponse = await _apiClient.HttpClient.GetAsync("/api/admin/manager/questions/categories");
            if (!httpResponse.IsSuccessStatusCode)
            {
                return [];
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<ApiResponse<List<QuestionBankCategoryViewModel>>>();
            return response?.Data ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }
}
