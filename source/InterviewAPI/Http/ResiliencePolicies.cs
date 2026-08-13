using Polly;
using Polly.Extensions.Http;

namespace InterviewAPI.Http;

/// <summary>
/// Circuit-breaker policies for this service's outbound HTTP dependencies.
///
/// WHY THIS IS A SINGLETON — do not change it to scoped/transient.
/// A circuit breaker counts consecutive failures inside the policy object itself. There must be
/// exactly ONE instance per dependency for the whole process, or the count resets and the breaker
/// can never reach its threshold. Registering the policy through AddPolicyHandler's per-request
/// overload would build a fresh breaker on every call — it would look wired up, log nothing, and
/// never open. Holding the policies on a singleton is what makes the state survive between calls.
///
/// There is deliberately NO retry here. Both dependencies are slow calls (ai-service plans a whole
/// question set; PaymentAPI moves credits), so an automatic retry either doubles the user's wait on
/// a dependency that is already failing, or — for deduct/refund — risks charging twice when the
/// first request actually succeeded and only its response was lost. Fast, honest failure is the
/// intended behaviour; the breaker exists to stop a dead dependency being hammered, not to hide it.
/// </summary>
public sealed class ResiliencePolicies
{
    // Consecutive failures before the circuit opens, and how long it stays open before letting a
    // single trial call through. 5/30s: long enough not to trip on one blip, short enough that
    // recovery is visible within a demo.
    private const int FailuresBeforeBreaking = 5;
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    public IAsyncPolicy<HttpResponseMessage> AiService { get; }
    public IAsyncPolicy<HttpResponseMessage> PaymentApi { get; }

    public ResiliencePolicies(ILoggerFactory loggerFactory, string aiServiceAddress, string paymentApiAddress)
    {
        AiService = BuildBreaker(
            loggerFactory.CreateLogger($"{nameof(ResiliencePolicies)}.AiService"),
            "ai-service",
            aiServiceAddress);

        PaymentApi = BuildBreaker(
            loggerFactory.CreateLogger($"{nameof(ResiliencePolicies)}.PaymentApi"),
            "PaymentAPI",
            paymentApiAddress);
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildBreaker(
        ILogger logger,
        string dependency,
        string address)
    {
        return HttpPolicyExtensions
            // 5xx, 408 and connection-level failures.
            .HandleTransientHttpError()
            // HttpClient.Timeout surfaces as TaskCanceledException with a TimeoutException inside.
            // A dependency that hangs is exactly what the breaker is for, so it must count. The
            // inner-exception check keeps genuine caller cancellations (user closed the tab,
            // request aborted) from counting as dependency failures.
            .Or<TaskCanceledException>(ex => ex.InnerException is TimeoutException)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: FailuresBeforeBreaking,
                durationOfBreak: BreakDuration,
                onBreak: (outcome, breakDelay) => logger.LogError(
                    "Circuit breaker OPEN for {Dependency} at {Address} — blocking calls for {BreakSeconds}s " +
                    "after {Failures} consecutive failures. Last failure: {Failure}",
                    dependency,
                    address,
                    breakDelay.TotalSeconds,
                    FailuresBeforeBreaking,
                    DescribeFailure(outcome)),
                onReset: () => logger.LogInformation(
                    "Circuit breaker CLOSED for {Dependency} at {Address} — the trial call succeeded, " +
                    "calls are flowing normally again.",
                    dependency,
                    address),
                onHalfOpen: () => logger.LogWarning(
                    "Circuit breaker HALF-OPEN for {Dependency} at {Address} — break expired, letting one " +
                    "trial call through to test whether it recovered.",
                    dependency,
                    address));
    }

    /// <summary>
    /// One readable reason string covering both failure shapes a policy can see: a thrown
    /// exception, or a response that came back with a handled status code.
    /// </summary>
    private static string DescribeFailure(DelegateResult<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
            return $"{outcome.Exception.GetType().Name}: {outcome.Exception.Message}";

        return outcome.Result is not null
            ? $"HTTP {(int)outcome.Result.StatusCode} {outcome.Result.ReasonPhrase}"
            : "unknown failure";
    }
}
