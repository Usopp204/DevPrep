namespace FeedbackAPI.Clients;

public abstract class InterviewApiException : Exception
{
    protected InterviewApiException(string message) : base(message) { }
}

public sealed class InterviewApiUnauthorizedException : InterviewApiException
{
    public InterviewApiUnauthorizedException() : base("InterviewAPI authentication failed.") { }
}

public sealed class InterviewApiForbiddenException : InterviewApiException
{
    public InterviewApiForbiddenException() : base("InterviewAPI forbids access.") { }
}

public sealed class InterviewApiNotFoundException : InterviewApiException
{
    public InterviewApiNotFoundException() : base("Session not found.") { }
}

public sealed class InterviewApiTimeoutException : InterviewApiException
{
    public InterviewApiTimeoutException() : base("InterviewAPI request timed out.") { }
}

public sealed class InterviewApiDependencyException : InterviewApiException
{
    public InterviewApiDependencyException() : base("InterviewAPI dependency failure.") { }
}