using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.Admin.TechStacks;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public sealed class AdminTechStackService : IAdminTechStackService
{
    private const string BasePath = "api/admin/tech-stacks";
    private const string ActiveDuplicateMessage = "A Tech Stack with this name already exists.";
    private const string InactiveDuplicateMessage =
        "A deactivated Tech Stack with this name already exists. Reactivate the existing record instead.";

    private static readonly HashSet<string> SafeValidationMessages = new(StringComparer.Ordinal)
    {
        "Name is required.",
        "Name must not exceed 200 characters.",
        "Description must not exceed 500 characters.",
        "IsActive is required.",
        "PageIndex must be at least 1.",
        "PageSize must be between 1 and 100.",
        "Status must be All, Active, or Inactive."
    };
    private readonly IApiClient _apiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminTechStackService(IApiClient apiClient, IHttpContextAccessor httpContextAccessor)
    {
        _apiClient = apiClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<AdminTechStackServiceResult<AdminTechStackPagedResponse>> GetListAsync(
        int pageIndex,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken = default)
    {
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

        var path = QueryHelpers.AddQueryString(BasePath, query);
        return SendAsync<AdminTechStackPagedResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, path),
            cancellationToken);
    }

    public Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> GetByIdAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminTechStackItemViewModel>(
            () => new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/{techStackId}"),
            cancellationToken);

    public Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> CreateAsync(
        CreateTechStackViewModel model,
        CancellationToken cancellationToken = default) =>
        SendAsync<AdminTechStackItemViewModel>(
            () => new HttpRequestMessage(HttpMethod.Post, BasePath)
            {
                Content = JsonContent.Create(new { model.Name, model.Description })
            },
            cancellationToken);

    public Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> UpdateAsync(
        Guid techStackId,
        UpdateTechStackViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.IsActive.HasValue)
        {
            return Task.FromResult(AdminTechStackServiceResult<AdminTechStackItemViewModel>.Failed(
                AdminTechStackResultStatus.ValidationFailure,
                "Status is required."));
        }

        return SendAsync<AdminTechStackItemViewModel>(
            () => new HttpRequestMessage(HttpMethod.Put, $"{BasePath}/{techStackId}")
            {
                Content = JsonContent.Create(new { model.Name, model.Description, IsActive = model.IsActive.Value })
            },
            cancellationToken);
    }

    public Task<AdminTechStackServiceResult<object?>> DeactivateAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default) =>
        SendAsync<object?>(
            () => new HttpRequestMessage(HttpMethod.Delete, $"{BasePath}/{techStackId}"),
            cancellationToken,
            allowNoContent: true);

    public async Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> ReactivateAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default)
    {
        var detailResult = await GetByIdAsync(techStackId, cancellationToken);
        if (!detailResult.IsSuccess || detailResult.Data is null)
        {
            return AdminTechStackServiceResult<AdminTechStackItemViewModel>.Failed(
                detailResult.Status,
                detailResult.Message);
        }

        var update = new UpdateTechStackViewModel
        {
            Name = detailResult.Data.Name,
            Description = detailResult.Data.Description,
            IsActive = true
        };

        return await UpdateAsync(techStackId, update, cancellationToken);
    }

    private async Task<AdminTechStackServiceResult<T>> SendAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken,
        bool allowNoContent = false)
    {
        var token = GetAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return AdminTechStackServiceResult<T>.Failed(
                AdminTechStackResultStatus.Unauthorized,
                "Your session has expired. Please sign in again.");
        }

        try
        {
            using var request = requestFactory();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _apiClient.HttpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (allowNoContent && response.StatusCode == HttpStatusCode.NoContent)
            {
                return AdminTechStackServiceResult<T>.Succeeded(default);
            }

            if (response.IsSuccessStatusCode)
            {
                var envelope = await ReadEnvelopeAsync<T>(response, cancellationToken);
                return envelope is { Success: true } && (envelope.Data is not null || allowNoContent)
                    ? AdminTechStackServiceResult<T>.Succeeded(envelope.Data, envelope.Message)
                    : AdminTechStackServiceResult<T>.Failed(
                        AdminTechStackResultStatus.UpstreamFailure,
                        envelope?.Message ?? "The Tech Stack service returned an invalid response.");
            }

            var error = await ReadEnvelopeAsync<object>(response, cancellationToken);
            return MapFailure<T>(response.StatusCode, error?.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpstreamFailure<T>();
        }
        catch (HttpRequestException)
        {
            return UpstreamFailure<T>();
        }
        catch (JsonException)
        {
            return UpstreamFailure<T>();
        }
        catch (NotSupportedException)
        {
            return UpstreamFailure<T>();
        }
    }

    private static async Task<ApiResponse<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken);
    }

    private string? GetAccessToken()
    {
        var cookies = _httpContextAccessor.HttpContext?.Request.Cookies;
        return cookies?["access_token"] ?? cookies?["JWToken"];
    }

    private static AdminTechStackServiceResult<T> MapFailure<T>(HttpStatusCode statusCode, string? message)
    {
        var status = statusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                AdminTechStackResultStatus.ValidationFailure,
            HttpStatusCode.Unauthorized => AdminTechStackResultStatus.Unauthorized,
            HttpStatusCode.Forbidden => AdminTechStackResultStatus.Forbidden,
            HttpStatusCode.NotFound => AdminTechStackResultStatus.NotFound,
            HttpStatusCode.Conflict => AdminTechStackResultStatus.DuplicateConflict,
            _ => AdminTechStackResultStatus.UpstreamFailure
        };

        return AdminTechStackServiceResult<T>.Failed(status, SafeMessage(status, message));
    }

    private static AdminTechStackServiceResult<T> UpstreamFailure<T>() =>
        AdminTechStackServiceResult<T>.Failed(
            AdminTechStackResultStatus.UpstreamFailure,
            "Tech Stack management is temporarily unavailable. Please try again.");

    private static string DefaultMessage(AdminTechStackResultStatus status) => status switch
    {
        AdminTechStackResultStatus.ValidationFailure => "Please check the submitted Tech Stack data.",
        AdminTechStackResultStatus.Unauthorized => "Your session has expired. Please sign in again.",
        AdminTechStackResultStatus.Forbidden => "You do not have permission to manage Tech Stacks.",
        AdminTechStackResultStatus.NotFound => "The Tech Stack could not be found.",
        AdminTechStackResultStatus.DuplicateConflict => "A Tech Stack with this name already exists.",
        _ => "Tech Stack management is temporarily unavailable. Please try again."
    };

    private static string SafeMessage(AdminTechStackResultStatus status, string? downstreamMessage)
    {
        if (status == AdminTechStackResultStatus.ValidationFailure &&
            downstreamMessage is not null &&
            SafeValidationMessages.Contains(downstreamMessage))
        {
            return downstreamMessage;
        }

        if (status == AdminTechStackResultStatus.DuplicateConflict)
        {
            return string.Equals(downstreamMessage, InactiveDuplicateMessage, StringComparison.Ordinal)
                ? InactiveDuplicateMessage
                : ActiveDuplicateMessage;
        }

        return DefaultMessage(status);
    }
}
