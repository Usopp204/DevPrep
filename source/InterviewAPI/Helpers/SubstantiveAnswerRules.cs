using InterviewAPI.Models;

namespace InterviewAPI.Helpers;

public static class SubstantiveAnswerRules
{
    public const string UserSenderType = "User";
    public const string AnswerMessageType = "Answer";
    public const string SkippedReason = "Skipped";
    public const string TimedOutReason = "TimedOut";

    public static bool IsSubstantive(ChatMessage message) =>
        message.SenderType == UserSenderType
        && message.MessageType == AnswerMessageType
        && message.AnswerReason is not SkippedReason and not TimedOutReason
        && !string.IsNullOrWhiteSpace(message.Content);

    public static bool HasSubstantiveContent(string? content) =>
        !string.IsNullOrWhiteSpace(content);
}
