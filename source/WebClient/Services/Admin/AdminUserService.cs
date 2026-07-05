using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Admin;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public interface IAdminUserService
{
    Task<AdminUserApiResponse<AdminUserPagedResult<AdminUserListItemDto>>> GetUsersAsync(AdminUserQueryDto query);

    Task<AdminUserApiResponse<AdminUserDetailDto>> GetUserDetailAsync(Guid id);

    Task<AdminUserApiResponse<object>> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto);

    Task<AdminUserApiResponse<object>> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto);
}

public sealed class AdminUserService(
    IApiClient apiClient,
    IHttpContextAccessor httpContextAccessor) : IAdminUserService
{
    public async Task<AdminUserApiResponse<AdminUserPagedResult<AdminUserListItemDto>>> GetUsersAsync(AdminUserQueryDto query)
    {
        if (query.Page <= 0)
            query.Page = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var queryParams = new Dictionary<string, string?>
        {
            ["keyword"] = query.Keyword,
            ["role"] = query.Role,
            ["status"] = query.Status,
            ["page"] = query.Page.ToString(),
            ["pageSize"] = query.PageSize.ToString()
        };

        if (query.CreatedFrom.HasValue)
            queryParams["createdFrom"] = query.CreatedFrom.Value.ToString("yyyy-MM-dd");

        if (query.CreatedTo.HasValue)
            queryParams["createdTo"] = query.CreatedTo.Value.ToString("yyyy-MM-dd");

        var requestUrl = QueryHelpers.AddQueryString("api/admin/users", queryParams);

        return await GetAsync<AdminUserPagedResult<AdminUserListItemDto>>(requestUrl);
    }

    public async Task<AdminUserApiResponse<AdminUserDetailDto>> GetUserDetailAsync(Guid id)
    {
        return await GetAsync<AdminUserDetailDto>($"api/admin/users/{id}");
    }

    public async Task<AdminUserApiResponse<object>> UpdateStatusAsync(Guid id, AdminUpdateUserStatusDto dto)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Patch,
            $"api/admin/users/{id}/status",
            dto
        );
    }

    public async Task<AdminUserApiResponse<object>> UpdateRoleAsync(Guid id, AdminUpdateUserRoleDto dto)
    {
        return await SendJsonAsync<object>(
            HttpMethod.Patch,
            $"api/admin/users/{id}/role",
            dto
        );
    }

    private async Task<AdminUserApiResponse<T>> GetAsync<T>(string requestUrl)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AdminUserApiResponse<T>
            {
                Success = false,
                Message = "Not authenticated."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await apiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new AdminUserApiResponse<T>
                {
                    Success = false,
                    Message = "Session expired. Please log in again."
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new AdminUserApiResponse<T>
                {
                    Success = false,
                    Message = "You do not have permission to access this resource."
                };
            }

            var body = await ReadBodyAsync<T>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
                return body;

            return new AdminUserApiResponse<T>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}."
            };
        }
        catch
        {
            return new AdminUserApiResponse<T>
            {
                Success = false,
                Message = "Cannot reach the server. Please make sure the API Gateway is running and try again."
            };
        }
    }

    private async Task<AdminUserApiResponse<TResponse>> SendJsonAsync<TResponse>(
        HttpMethod method,
        string requestUrl,
        object payload)
    {
        var token = GetAccessToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AdminUserApiResponse<TResponse>
            {
                Success = false,
                Message = "Not authenticated."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(method, requestUrl)
            {
                Content = JsonContent.Create(payload)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await apiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new AdminUserApiResponse<TResponse>
                {
                    Success = false,
                    Message = "Session expired. Please log in again."
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new AdminUserApiResponse<TResponse>
                {
                    Success = false,
                    Message = "You do not have permission to perform this action."
                };
            }

            var body = await ReadBodyAsync<TResponse>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
                return body;

            return new AdminUserApiResponse<TResponse>
            {
                Success = false,
                Message = body?.Message ?? $"Request failed HTTP {(int)response.StatusCode}."
            };
        }
        catch
        {
            return new AdminUserApiResponse<TResponse>
            {
                Success = false,
                Message = "Cannot reach the server. Please make sure the API Gateway is running and try again."
            };
        }
    }

    private string? GetAccessToken()
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
            return null;

        if (httpContext.Request.Cookies.TryGetValue("access_token", out var accessToken))
            return accessToken;

        if (httpContext.Request.Cookies.TryGetValue("JWToken", out var jwtToken))
            return jwtToken;

        return null;
    }

    private static async Task<AdminUserApiResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<AdminUserApiResponse<T>>();
        }
        catch
        {
            return null;
        }
    }
}