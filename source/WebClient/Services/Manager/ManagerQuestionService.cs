using WebClient.Models.Manager;
using WebClient.Models.Shared;
using WebClient.Services.Shared;
using Microsoft.AspNetCore.WebUtilities;

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
        
        var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<PagedResult<QuestionBankListItemViewModel>>>(requestUrl);
        return response?.Data ?? new PagedResult<QuestionBankListItemViewModel>();
    }

    public async Task<QuestionBankDetailViewModel?> GetByIdAsync(Guid id)
    {
        var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<QuestionBankDetailViewModel>>($"/api/admin/manager/questions/{id}");
        return response?.Data;
    }
}
