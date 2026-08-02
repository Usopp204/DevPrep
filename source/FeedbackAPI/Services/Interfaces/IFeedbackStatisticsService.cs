using FeedbackAPI.DTOs;

namespace FeedbackAPI.Services.Interfaces;

public interface IFeedbackStatisticsService
{
    Task<FeedbackDashboardStatisticsDto> GetDashboardStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}