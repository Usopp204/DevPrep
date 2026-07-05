using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Manager;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Manager;

public class ManagerNewsService : IManagerNewsService
{
    private readonly IApiClient _apiClient;

    public ManagerNewsService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ManagerNewsPagedResult<ManagerNewsListItemViewModel>> GetListAsync(
        int page = 1,
        int pageSize = 10,
        string? keyword = null,
        string? status = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrWhiteSpace(keyword)) query["keyword"] = keyword;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;

        var url = QueryHelpers.AddQueryString("/api/admin/manager/news", query);
        var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<ManagerNewsPagedResult<ManagerNewsListItemViewModel>>>(url);
        return response?.Data ?? new ManagerNewsPagedResult<ManagerNewsListItemViewModel>();
    }

    public async Task<ManagerNewsDetailViewModel?> GetByIdAsync(Guid id)
    {
        var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<ManagerNewsDetailViewModel>>($"/api/admin/manager/news/{id}");
        return response?.Data;
    }
}
