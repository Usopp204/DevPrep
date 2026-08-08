using FeedbackAPI.DTOs;
using FeedbackAPI.Repositories.Interfaces;
using FeedbackAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAPI.Services;

public sealed class FeedbackStatisticsService(
    ISystemFeedbackRepository systemFeedbackRepository,
    IAiFeedbackRepository aiFeedbackRepository) : IFeedbackStatisticsService
{
    public async Task<FeedbackDashboardStatisticsDto> GetDashboardStatisticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var rangeFrom = from.ToDateTime(TimeOnly.MinValue);
        var rangeToExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var systemFeedbacks = systemFeedbackRepository.QueryIncludingDeleted();
        var interviewFeedbacks = aiFeedbackRepository.QueryIncludingDeleted();

        var totalSystemFeedbacksAsOf = await systemFeedbacks.LongCountAsync(
            feedback => feedback.CreatedAt < rangeToExclusive,
            cancellationToken);
        var totalInterviewFeedbacksAsOf = await interviewFeedbacks.LongCountAsync(
            feedback => feedback.CreatedAt < rangeToExclusive,
            cancellationToken);
        var systemByDate = await systemFeedbacks
            .Where(feedback => feedback.CreatedAt >= rangeFrom && feedback.CreatedAt < rangeToExclusive)
            .GroupBy(feedback => feedback.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);
        var interviewByDate = await interviewFeedbacks
            .Where(feedback => feedback.CreatedAt >= rangeFrom && feedback.CreatedAt < rangeToExclusive)
            .GroupBy(feedback => feedback.CreatedAt.Date)
            .Select(group => new { Date = group.Key, Count = group.LongCount() })
            .ToListAsync(cancellationToken);

        var systemCounts = systemByDate.ToDictionary(
            item => DateOnly.FromDateTime(item.Date),
            item => item.Count);
        var interviewCounts = interviewByDate.ToDictionary(
            item => DateOnly.FromDateTime(item.Date),
            item => item.Count);
        var days = new List<FeedbackDashboardStatisticsDayDto>(to.DayNumber - from.DayNumber + 1);
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var systemCount = systemCounts.GetValueOrDefault(date);
            var interviewCount = interviewCounts.GetValueOrDefault(date);
            days.Add(new FeedbackDashboardStatisticsDayDto
            {
                Date = date,
                SystemFeedbacks = systemCount,
                InterviewFeedbacks = interviewCount,
                TotalFeedbacks = checked(systemCount + interviewCount)
            });
        }

        return new FeedbackDashboardStatisticsDto
        {
            From = from,
            To = to,
            TotalSystemFeedbacksAsOf = totalSystemFeedbacksAsOf,
            TotalInterviewFeedbacksAsOf = totalInterviewFeedbacksAsOf,
            TotalFeedbacksAsOf = checked(totalSystemFeedbacksAsOf + totalInterviewFeedbacksAsOf),
            Days = days
        };
    }
}