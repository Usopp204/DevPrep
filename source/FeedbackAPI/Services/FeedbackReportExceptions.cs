namespace FeedbackAPI.Services;

public sealed class FeedbackReportValidationException : Exception
{
    public FeedbackReportValidationException(string message) : base(message) { }
}

public sealed class FeedbackReportNotFoundException : Exception
{
    public FeedbackReportNotFoundException(string message = "Feedback report was not found.") : base(message) { }
}

public sealed class FeedbackReportForbiddenException : Exception
{
    public FeedbackReportForbiddenException(string message) : base(message) { }
}

public sealed class DuplicateFeedbackReportException : Exception
{
    public DuplicateFeedbackReportException(string message = "You have already reported this feedback.") : base(message) { }
}

public sealed class FeedbackReportConflictException : Exception
{
    public FeedbackReportConflictException(string message) : base(message) { }
}
