using System.Text.Json;
using EvaluationAPI.Clients;
using EvaluationAPI.DTOs.AiService;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Helpers;
using EvaluationAPI.Models;
using EvaluationAPI.Repositories.Interfaces;
using EvaluationAPI.Services.Interfaces;

namespace EvaluationAPI.Services;

public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _repo;
    private readonly IAiServiceClient _aiServiceClient;

    public EvaluationService(IEvaluationRepository repo, IAiServiceClient aiServiceClient)
    {
        _repo = repo;
        _aiServiceClient = aiServiceClient;
    }

    public async Task<EvaluateAnswerResponse> EvaluateAnswerAsync(
        Guid sessionId, Guid answerId, EvaluateAnswerRequest req, CancellationToken ct)
    {
        // Step 0: Idempotency — if this answer was already graded, return the existing evaluation.
        // This prevents duplicate rows (which would skew the report) and avoids a wasted ai-service call.
        var existing = await _repo.GetBySessionQuestionAsync(sessionId, answerId, ct);
        if (existing is not null)
            return ToResponse(existing);

        // Step A: Gọi ai-service chấm điểm một câu trả lời.
        var response = await _aiServiceClient.EvaluateAnswerAsync(new AiEvaluateAnswerRequest
        {
            Question = req.Question,
            ReferenceAnswer = req.ReferenceAnswer,
            Category = req.Category,
            Difficulty = req.Difficulty,
            UserAnswer = req.UserAnswer,
            Followups = new List<object>()
        }, ct);

        // Step B: Score null = ai-service chấm thất bại (rate limit...) — báo lỗi để client thử lại sau.
        if (response.Score is null)
            throw new InvalidOperationException("Grading failed, please try again.");

        // Step C: Lưu kết quả chấm vào AnswerEvaluations.
        var eval = new AnswerEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SessionId = sessionId,
            SessionQuestionId = answerId,
            QuestionContent = req.Question,
            UserAnswer = req.UserAnswer,
            Score = (decimal)response.Score.Value,
            CriteriaScoresJson = JsonSerializer.Serialize(response.Criteria),
            Strengths = response.Praise,
            Weaknesses = response.Analysis, // tạm lưu toàn bộ phân tích vào Weaknesses
            AiFeedback = response.Analysis,
            ModelUsed = response.ModelUsed,
            GeneratedAt = VietnamTime.Now
        };
        await _repo.AddEvaluationAsync(eval, ct);

        // Step D: Trả về DTO từ entity đã lưu.
        return ToResponse(eval);
    }

    private static EvaluateAnswerResponse ToResponse(AnswerEvaluation e) => new()
    {
        EvaluationId = e.EvaluationId,
        SessionId = e.SessionId,
        SessionQuestionId = e.SessionQuestionId,
        Score = e.Score,
        CriteriaScoresJson = e.CriteriaScoresJson,
        Strengths = e.Strengths,
        AiFeedback = e.AiFeedback,
        ModelUsed = e.ModelUsed
    };

    public async Task<ReportResponse> BuildReportAsync(
        Guid sessionId, BuildReportRequest req, CancellationToken ct)
    {
        // Step A: Không tạo lại báo cáo nếu đã có.
        if (await _repo.ReportExistsAsync(sessionId, ct))
            throw new InvalidOperationException("Report already generated.");

        // Step B: Lấy tất cả đánh giá từng câu của phiên.
        var evaluations = await _repo.GetEvaluationsBySessionAsync(sessionId, ct);
        if (evaluations.Count == 0)
            throw new InvalidOperationException("No evaluations found for this session.");

        // Step C: Dựng request tổng hợp gửi ai-service.
        var aiRequest = new AiBuildReportRequest
        {
            DurationActualSec = req.DurationActualSec,
            Evaluations = evaluations.Select(e => new AiEvalRow
            {
                Category = e.QuestionContent ?? "",
                Score = (double)e.Score,
                Criteria = string.IsNullOrEmpty(e.CriteriaScoresJson)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, double>>(e.CriteriaScoresJson)
            }).ToList()
        };

        // Step D: ai-service tổng hợp ra báo cáo cuối.
        var aiResponse = await _aiServiceClient.BuildReportAsync(aiRequest, ct);

        var overallScore = aiResponse.OverallScore;

        // Category scores come from the rubric axes stored per answer (knowledge / problem_solving /
        // communication), NOT from question-topic categories. Real categories are React, PostgreSQL,
        // ... — never "Backend" — so the old name lookup always fell back to overall.
        var criteria = evaluations
            .Where(e => !string.IsNullOrEmpty(e.CriteriaScoresJson))
            .Select(e => JsonSerializer.Deserialize<Dictionary<string, double>>(e.CriteriaScoresJson!))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();

        decimal? AxisAverage(string axis)
        {
            var values = criteria.Where(c => c.ContainsKey(axis)).Select(c => c[axis]).ToList();
            return values.Count == 0 ? null : (decimal)Math.Round(values.Average());
        }

        var technicalScore = AxisAverage("knowledge");
        var communicationScore = AxisAverage("communication");
        var problemSolvingScore = AxisAverage("problem_solving");

        // Step E: Lưu FeedbackReport.
        var report = new FeedbackReport
        {
            ReportId = Guid.NewGuid(),
            SessionId = sessionId,
            OverallScore = (decimal)overallScore,
            TechnicalScore = technicalScore,           // knowledge axis
            TheoryScore = null,                        // no theory axis in the rubric — dropped
            CommunicationScore = communicationScore,   // communication axis
            ProblemSolvingScore = problemSolvingScore, // problem-solving axis
            Summary = aiResponse.Summary?.Praise ?? "",
            Strengths = string.Join("; ", aiResponse.Summary?.Strengths ?? new List<string>()),
            Improvements = string.Join("; ", aiResponse.Summary?.AreasToImprove ?? new List<string>()),
            Recommendations = "Review the areas above and practice regularly.",
            Grade = overallScore >= 85 ? "A" : overallScore >= 70 ? "B" : overallScore >= 55 ? "C" : "D",
            GeneratedAt = VietnamTime.Now
        };
        await _repo.AddReportAsync(report, ct);

        // Step F: Lưu chỉ số hiệu năng để thống kê sau này.
        var metrics = new List<PerformanceMetric>
        {
            new()
            {
                MetricId = Guid.NewGuid(),
                SessionId = sessionId,
                MetricName = "OverallScore",
                MetricValue = (decimal)overallScore,
                RecordedAt = VietnamTime.Now
            }
        };
        if (technicalScore is not null)
        {
            metrics.Add(new PerformanceMetric
            {
                MetricId = Guid.NewGuid(),
                SessionId = sessionId,
                MetricName = "TechnicalScore",
                MetricValue = technicalScore.Value,
                RecordedAt = VietnamTime.Now
            });
        }
        await _repo.AddMetricsAsync(metrics, ct);

        // Step G: Trả về DTO từ báo cáo đã lưu.
        return new ReportResponse
        {
            ReportId = report.ReportId,
            SessionId = report.SessionId,
            OverallScore = report.OverallScore,
            TechnicalScore = report.TechnicalScore,
            TheoryScore = report.TheoryScore,
            CommunicationScore = report.CommunicationScore,
            ProblemSolvingScore = report.ProblemSolvingScore,
            Summary = report.Summary,
            Strengths = report.Strengths,
            Improvements = report.Improvements,
            Recommendations = report.Recommendations,
            Grade = report.Grade,
            GeneratedAt = report.GeneratedAt
        };
    }

    public async Task<ReportResponse?> GetExistingReportAsync(Guid sessionId, CancellationToken ct)
    {
        var report = await _repo.GetReportBySessionAsync(sessionId, ct);
        if (report is null) return null;

        return new ReportResponse
        {
            ReportId = report.ReportId,
            SessionId = report.SessionId,
            OverallScore = report.OverallScore,
            TechnicalScore = report.TechnicalScore,
            TheoryScore = report.TheoryScore,
            CommunicationScore = report.CommunicationScore,
            ProblemSolvingScore = report.ProblemSolvingScore,
            Summary = report.Summary,
            Strengths = report.Strengths,
            Improvements = report.Improvements,
            Recommendations = report.Recommendations,
            Grade = report.Grade,
            GeneratedAt = report.GeneratedAt
        };
    }

    public async Task<List<EvaluateAnswerResponse>> GetAnswerEvaluationsAsync(Guid sessionId, CancellationToken ct)
    {
        var evaluations = await _repo.GetEvaluationsBySessionAsync(sessionId, ct);
        return evaluations.Select(e => new EvaluateAnswerResponse
        {
            EvaluationId = e.EvaluationId,
            SessionId = e.SessionId,
            SessionQuestionId = e.SessionQuestionId,
            Score = e.Score,
            CriteriaScoresJson = e.CriteriaScoresJson,
            Strengths = e.Strengths,
            AiFeedback = e.AiFeedback,
            ModelUsed = e.ModelUsed
        }).ToList();
    }
}
