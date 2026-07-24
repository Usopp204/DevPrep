using FeedbackAPI.Services;

namespace FeedbackAPI.Validation;

public static class InterviewFeedbackRules
{
    public const int MaxCommentLength = 1000;
}

public static class SystemFeedbackRules
{
    public const int MaxCommentLength = 4000;
}

internal static class FeedbackInput
{
    internal static string? NormalizeOptionalComment(string? comment, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        if (comment.Length > maxLength)
        {
            throw new FeedbackValidationException($"Comment cannot exceed {maxLength} characters.");
        }

        return comment;
    }
}