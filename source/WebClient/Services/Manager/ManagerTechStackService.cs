using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Manager.TechStacks;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Manager;

public sealed class ManagerTechStackService : IManagerTechStackService
{
    private const string ListPath = "api/admin/manager/tech-stacks";
    private readonly IApiClient _apiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ManagerTechStackService(
        IApiClient apiClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _apiClient = apiClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ManagerTechStackServiceResult<ManagerTechStackPagedResponse>> GetListAsync(
        int pageIndex,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken = default)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Failure(ManagerTechStackResultStatus.Unauthorized);
        }

        var query = new Dictionary<string, string?>
        {
            ["pageIndex"] = Math.Max(1, pageIndex).ToString(),
            ["pageSize"] = Math.Max(1, pageSize).ToString(),
            ["status"] = status
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query["search"] = search.Trim();
        }

        var requestPath = QueryHelpers.AddQueryString(ListPath, query);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestPath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _apiClient.HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Failure(MapStatus(response.StatusCode));
            }

            var envelope = await response.Content.ReadFromJsonAsync<
                ApiResponse<ManagerTechStackPagedResponse>>(cancellationToken: cancellationToken);

            return envelope is { Success: true, Data: not null }
                ? ManagerTechStackServiceResult<ManagerTechStackPagedResponse>.Succeeded(envelope.Data)
                : Failure(ManagerTechStackResultStatus.UpstreamFailure);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(ManagerTechStackResultStatus.UpstreamFailure);
        }
        catch (HttpRequestException)
        {
            return Failure(ManagerTechStackResultStatus.UpstreamFailure);
        }
        catch (JsonException)
        {
            return Failure(ManagerTechStackResultStatus.UpstreamFailure);
        }
        catch (NotSupportedException)
        {
            return Failure(ManagerTechStackResultStatus.UpstreamFailure);
        }
    }

    private string? GetAccessToken()
    {
        var cookies = _httpContextAccessor.HttpContext?.Request.Cookies;
        return cookies?["access_token"] ?? cookies?["JWToken"];
    }

    private static ManagerTechStackResultStatus MapStatus(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                ManagerTechStackResultStatus.ValidationFailure,
            HttpStatusCode.Unauthorized => ManagerTechStackResultStatus.Unauthorized,
            HttpStatusCode.Forbidden => ManagerTechStackResultStatus.Forbidden,
            _ => ManagerTechStackResultStatus.UpstreamFailure
        };

    private static ManagerTechStackServiceResult<ManagerTechStackPagedResponse> Failure(
        ManagerTechStackResultStatus status) =>
        ManagerTechStackServiceResult<ManagerTechStackPagedResponse>.Failed(
            status,
            status switch
            {
                ManagerTechStackResultStatus.ValidationFailure =>
                    "The Tech Stack query is invalid. Please reset the filters and try again.",
                ManagerTechStackResultStatus.Unauthorized =>
                    "Your session has expired. Please sign in again.",
                ManagerTechStackResultStatus.Forbidden =>
                    "You do not have permission to view Manager Tech Stacks.",
                _ => "Tech Stacks are temporarily unavailable. Please try again."
            });
}
