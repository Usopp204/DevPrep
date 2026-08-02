using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StatisticsAPI.Clients;
using StatisticsAPI.Data;
using StatisticsAPI.DTOs;
using StatisticsAPI.Helpers;
using StatisticsAPI.Models;

namespace StatisticsAPI.Services;

public interface IStatisticsCollector
{
    Task<RebuildResultDto> RunAsync(bool waitForAvailability, CancellationToken cancellationToken = default);
}

public sealed class StatisticsCollector(
    StatisticsDbContext db,
    ISourceMetricsClient source,
    StatisticsCollectionGate collectionGate,
    ILogger<StatisticsCollector> logger) : IStatisticsCollector
{
    public async Task<RebuildResultDto> RunAsync(
        bool waitForAvailability,
        CancellationToken cancellationToken = default)
    {
        using var lease = await collectionGate.EnterAsync(waitForAvailability, cancellationToken);
        if (lease is null)
        {
            return new RebuildResultDto
            {
                Busy = true,
                FinishedAt = VietnamTime.Now,
                Warnings = ["A statistics collection is already in progress."]
            };
        }

        var generationTime = VietnamTime.Now;
        var to = DateOnly.FromDateTime(generationTime);
        var from = to.AddDays(-365);

        try
        {
            var candidatesTask = source.GetCandidateStatisticsAsync(from, to, cancellationToken);
            var interviewsTask = source.GetInterviewDashboardStatisticsAsync(from, to, cancellationToken);
            var revenueTask = source.GetRevenueDailyAsync(from, to, cancellationToken);
            var feedbackTask = source.GetFeedbackDashboardStatisticsAsync(from, to, cancellationToken);
            await Task.WhenAll(candidatesTask, interviewsTask, revenueTask, feedbackTask);

            var candidates = await candidatesTask;
            var interviews = await interviewsTask;
            var revenue = await revenueTask;
            var feedback = await feedbackTask;

            var candidatesByDay = candidates.Days.ToDictionary(day => day.Date, day => checked((int)day.Count));
            var interviewsByDay = interviews.Days.ToDictionary(day => day.Date, day => checked((int)day.Count));
            var revenueByDay = revenue.Days.ToDictionary(day => day.Date);
            var feedbackByDay = feedback.Days.ToDictionary(day => day.Date, day => checked((int)day.TotalFeedbacks));

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var counters = await db.MetricCounters.ToDictionaryAsync(counter => counter.MetricKey, cancellationToken);

                void SetCounter(string key, decimal value)
                {
                    if (!counters.TryGetValue(key, out var counter))
                    {
                        counter = new MetricCounter { MetricKey = key };
                        counters.Add(key, counter);
                        db.MetricCounters.Add(counter);
                    }

                    counter.MetricValue = value;
                    counter.UpdatedAt = generationTime;
                }

                SetCounter(MetricKeys.DashboardTotalCandidates, candidates.TotalCandidates);
                SetCounter(MetricKeys.DashboardTotalInterviewSessions, interviews.TotalInterviewSessions);
                SetCounter(MetricKeys.DashboardCompletedInterviewSessions, interviews.CompletedInterviewSessions);
                SetCounter(MetricKeys.DashboardTotalFeedbacks, feedback.AllTimeTotalFeedbacks);
                SetCounter(MetricKeys.DashboardGeneration, 1m);
                SetCounter(MetricKeys.DashboardWindowFrom, DateKey(from));
                SetCounter(MetricKeys.DashboardWindowTo, DateKey(to));

                var existing = await db.StatisticsSnapshots
                    .Where(snapshot => snapshot.SnapshotDate >= from && snapshot.SnapshotDate <= to)
                    .ToDictionaryAsync(snapshot => snapshot.SnapshotDate, cancellationToken);

                for (var date = from; date <= to; date = date.AddDays(1))
                {
                    if (!existing.TryGetValue(date, out var snapshot))
                    {
                        snapshot = new StatisticsSnapshot
                        {
                            SnapshotId = Guid.NewGuid(),
                            SnapshotDate = date,
                            CreatedAt = generationTime
                        };
                        db.StatisticsSnapshots.Add(snapshot);
                    }

                    var revenueDay = revenueByDay[date];
                    snapshot.NewUsers = candidatesByDay[date];
                    snapshot.InterviewSessions = interviewsByDay[date];
                    snapshot.Revenue = revenueDay.Revenue;
                    snapshot.PaidOrders = checked((int)revenueDay.Orders);
                    snapshot.NewFeedbacks = feedbackByDay[date];
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            logger.LogInformation(
                "Dashboard statistics generation committed at {GenerationTime}: {SnapshotCount} daily rows from all four required sources.",
                generationTime,
                366);

            return new RebuildResultDto
            {
                Ok = true,
                SnapshotsWritten = 366,
                CountersWritten = 7,
                FinishedAt = generationTime
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Dashboard statistics generation failed; the prior generation was preserved.");
            return new RebuildResultDto
            {
                FinishedAt = generationTime,
                Warnings = ["Dashboard statistics could not be refreshed. The previous generation was preserved."]
            };
        }
    }

    private static decimal DateKey(DateOnly date) =>
        decimal.Parse(date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}