namespace WebClient.Models.Feedback;

public static class InterviewFeedbackRules
{
    public const int MaxCommentLength = 1000;
}

public static class SystemFeedbackRules
{
    public const int MaxCommentLength = 4000;
}

public static class FeedbackInput
{
    public static string? NormalizeOptionalComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment;
}

public static class FeedbackTempDataKeys
{
    public const string InterviewForm = "InterviewFeedbackForm";
    public const string InterviewValidation = "InterviewFeedbackValidation";
    public const string InterviewEditForm = "InterviewFeedbackEditForm";
    public const string InterviewEditFeedbackId = "InterviewFeedbackEditFeedbackId";
    public const string SystemForm = "SystemFeedbackForm";
    public const string SystemValidation = "SystemFeedbackValidation";
}