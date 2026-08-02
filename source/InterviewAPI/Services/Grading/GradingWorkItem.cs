using InterviewAPI.DTOs.Evaluation;

namespace InterviewAPI.Services.Grading;

public sealed record GradingWorkItem(
    Guid SessionId,
    Guid SessionQuestionId,
    Guid ChatMessageId,
    Guid UserId,
    EvaluateAnswerPayload Payload,
    string? BearerToken);