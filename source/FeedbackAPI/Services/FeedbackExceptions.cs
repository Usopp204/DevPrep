namespace FeedbackAPI.Services;

public sealed class FeedbackValidationException : Exception
{
    public FeedbackValidationException(string message) : base(message) { }
}

public sealed class FeedbackEligibilityException : Exception
{
    public FeedbackEligibilityException(string message) : base(message) { }
}

public sealed class DuplicateFeedbackException : Exception
{
    public DuplicateFeedbackException(string message = "Feedback already exists for this interview session.") : base(message) { }
}

public sealed class FeedbackNotFoundException : Exception
{
    public FeedbackNotFoundException(string message = "Feedback not found.") : base(message) { }
}