namespace InterviewAPI.Services;

// Dependency-failure exceptions for the interview session flow. Each one exists so a controller can
// answer 503 ("our side, retry shortly") instead of letting a raw dependency error escape as a 400
// with internal text in it. Shape copied from EvaluationAPI's GradingExceptions.cs on purpose.

/// <summary>
/// A dependency needed to start an interview session was unavailable (today: ai-service failing to
/// plan the question set, including the case where its circuit breaker is open).
///
/// Deliberately mirrors EvaluationAPI's EvaluationDependencyException — same sealed shape, same
/// optional inner exception, same "temporarily unavailable" message — so both of this project's
/// services report an unavailable dependency the same way and a reader who has seen one recognises
/// the other. The controller maps this to 503, separating "the dependency is down" from the 400
/// that a genuinely invalid request deserves.
/// </summary>
public sealed class SessionInitDependencyException : Exception
{
    public SessionInitDependencyException(Exception? innerException = null)
        : base("Interview setup is temporarily unavailable.", innerException) { }
}

/// <summary>
/// PaymentAPI could not be reached while charging or refunding a session's credits — as opposed to
/// PaymentAPI answering and refusing (not enough credits), which is a normal business outcome and
/// keeps its own message.
///
/// Exists because the previous code threw
/// `InvalidOperationException($"Credit deduction failed: {deductResult.Message}")` for BOTH cases,
/// and the controller echoed that message to the browser — so a PaymentAPI outage showed the user
/// "Credit deduction failed: Error: Name or service not known (paymentapi:8080)", leaking an
/// internal hostname and port. The transport detail now stays in the server log; this exception
/// carries only what a user should see.
/// </summary>
public sealed class CreditServiceUnavailableException : Exception
{
    public CreditServiceUnavailableException()
        : base("The payment service is temporarily unavailable.") { }
}
