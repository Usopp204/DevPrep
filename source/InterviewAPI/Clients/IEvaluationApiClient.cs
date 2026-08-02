using InterviewAPI.DTOs.Evaluation;

namespace InterviewAPI.Clients;

public interface IEvaluationApiClient
{
    // Grades one answer by calling EvaluationAPI. The user's bearer token is forwarded so
    // EvaluationAPI runs under the same identity ([Authorize]).
    Task<EvaluateAnswerResult> EvaluateAnswerAsync(
        Guid sessionId, Guid sessionQuestionId, EvaluateAnswerPayload payload,
        string? bearerToken, CancellationToken ct = default);
}