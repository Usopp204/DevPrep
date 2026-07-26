using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Manager;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Manager;

public class ManagerUserService : IManagerUserService
{
    private readonly IApiClient _apiClient;

    public ManagerUserService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<PagedResult<UserListItemViewModel>> GetListAsync(int page = 1, int pageSize = 10, string? keyword = null, string? role = null, string? status = null, DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        var url = "/api/admin/manager/users";
        var queryParams = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        if (!string.IsNullOrEmpty(keyword)) queryParams["keyword"] = keyword;
        if (!string.IsNullOrEmpty(role)) queryParams["role"] = role;
        if (!string.IsNullOrEmpty(status)) queryParams["status"] = status;
        if (createdFrom.HasValue) queryParams["createdFrom"] = createdFrom.Value.ToString("yyyy-MM-dd");
        if (createdTo.HasValue) queryParams["createdTo"] = createdTo.Value.ToString("yyyy-MM-dd");

        var requestUrl = QueryHelpers.AddQueryString(url, queryParams);
        
        try
        {
            var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<PagedResult<UserListItemViewModel>>>(requestUrl);
            return response?.Data ?? new PagedResult<UserListItemViewModel>();
        }
        catch (Exception)
        {
            return new PagedResult<UserListItemViewModel>();
        }
    }

    public async Task<UserDetailsViewModel?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetFromJsonAsync<ApiResponse<UserDetailsViewModel>>($"/api/admin/manager/users/{id}");
            return response?.Data;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
