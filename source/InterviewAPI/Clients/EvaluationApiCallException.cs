using System.Net;

namespace InterviewAPI.Clients;

public enum EvaluationApiFailureCategory
{
    Transport,
    Timeout,
    RateLimited,
    DependencyUnavailable,
    InvalidDependencyResponse,
    InvalidRequest,
    Authentication,
    Ownership,
    NotFound
}

public sealed class EvaluationApiCallException : Exception
{
    public EvaluationApiCallException(
        EvaluationApiFailureCategory category,
        bool isRetryable,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base("The answer evaluation request did not complete.", innerException)
    {
        Category = category;
        IsRetryable = isRetryable;
        StatusCode = statusCode;
    }

    public EvaluationApiFailureCategory Category { get; }
    public bool IsRetryable { get; }
    public HttpStatusCode? StatusCode { get; }
}