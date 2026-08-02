using System.Net.Http.Json;
using System.Text.Json;
using WebClient.Models.Dashboard;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Dashboard;

public interface IManagementDashboardService
{
    Task<ApiCallResult<ManagementDashboardData>> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public sealed class ManagementDashboardService(
    IApiClient apiClient,
    ILogger<ManagementDashboardService> logger) : IManagementDashboardService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiCallResult<ManagementDashboardData>> GetDashboardAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await apiClient.HttpClient.GetAsync(
                $"api/admin/statistics/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
                cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ManagementDashboardData>>(
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode || envelope?.Success != true || envelope.Data is null)
            {
                return ApiCallResult<ManagementDashboardData>.Fail(
                    envelope?.Message ?? "Could not load dashboard statistics.",
                    (int)response.StatusCode);
            }

            return ApiCallResult<ManagementDashboardData>.Ok(envelope.Data, (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException exception)
        {
            logger.LogWarning(exception, "Dashboard statistics request timed out.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Dashboard statistics are unreachable.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status502BadGateway);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Dashboard statistics returned malformed JSON.");
            return ApiCallResult<ManagementDashboardData>.Fail(
                "Could not load dashboard statistics.",
                StatusCodes.Status502BadGateway);
        }
    }
}