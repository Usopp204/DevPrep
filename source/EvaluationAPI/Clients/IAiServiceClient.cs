using EvaluationAPI.DTOs.AiService;

namespace EvaluationAPI.Clients;

public interface IAiServiceClient
{
    Task<AiQuestionPlanResponse> PlanQuestionsAsync(AiQuestionPlanRequest request, CancellationToken ct = default);
    Task<AiEvaluateAnswerResponse> EvaluateAnswerAsync(AiEvaluateAnswerRequest request, CancellationToken ct = default);
}
