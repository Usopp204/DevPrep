using Polly;
using Polly.Extensions.Http;

namespace EvaluationAPI.Http;

/// <summary>
/// Circuit-breaker (and, for InterviewAPI, retry) policies for this service's outbound HTTP calls.
///
/// DELIBERATELY DUPLICATED from InterviewAPI/Http/ResiliencePolicies.cs rather than shared.
/// The two services own their own resilience posture: EvaluationAPI retries its idempotent GETs
/// against InterviewAPI, InterviewAPI retries nothing, and the two will drift further as each
/// learns what its own dependencies do. A shared package would couple their release cycles and
/// force one service's tuning onto the other for the sake of ~60 lines. Honest duplication across
/// a service boundary beats a shared library that quietly becomes a distributed monolith.
/// The log MESSAGES are kept identical on purpose so breaker transitions read the same in both
/// services' logs — if you change the wording here, change it there too.
///
/// WHY THIS IS A SINGLETON — do not change it to scoped/transient.
/// A circuit breaker counts consecutive failures inside the policy object itself. There must be
/// exactly ONE instance per dependency for the whole process, or the count resets and the breaker
/// can never reach its threshold. Registering through AddPolicyHandler's per-request overload
/// would build a fresh breaker on every call — it would look wired up, log nothing, and never open.
/// </summary>
public sealed class ResiliencePolicies
{
    // Consecutive failures before the circuit opens, and how long it stays open before letting a
    // single trial call through. Matches InterviewAPI so both services behave the same under load.
    private const int FailuresBeforeBreaking = 5;
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Breaker only, no retry. Grading and report-building calls are slow (60s timeout) and
    /// expensive on the GPU; an automatic retry doubles a user's wait on a dependency that has
    /// already failed, and ai-service is the single busiest resource in the system.
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> AiService { get; }

    /// <summary>
    /// Retry once, then the breaker. Every call this client makes is a side-effect-free GET —
    /// verified: OwnsSessionAsync hits GET /owns (SessionService.OwnsSessionAsync is a single
    /// repository read plus a comparison) and GetSessionEvaluationContextAsync hits GET /transcript
    /// (a read plus a projection). Neither writes, neither calls SaveChangesAsync, so re-issuing
    /// one is safe. They are also short (15s timeout) and sit in front of a user-visible report, so
    /// riding out a single blip is worth ~300ms.
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> InterviewApi { get; }

    public ResiliencePolicies(ILoggerFactory loggerFactory, string aiServiceAddress, string interviewApiAddress)
    {
        AiService = BuildBreaker(
            loggerFactory.CreateLogger($"{nameof(ResiliencePolicies)}.AiService"),
            "ai-service",
            aiServiceAddress);

        var interviewLogger = loggerFactory.CreateLogger($"{nameof(ResiliencePolicies)}.InterviewApi");

        // Retry is the OUTER policy so each individual attempt is counted by the breaker underneath
        // it. HandleTransientHttpError does not handle BrokenCircuitException, so once the circuit
        // is open the retry does not burn an attempt hammering it — the rejection propagates.
        InterviewApi = Policy.WrapAsync(
            BuildRetry(interviewLogger, "InterviewAPI", interviewApiAddress),
            BuildBreaker(interviewLogger, "InterviewAPI", interviewApiAddress));
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetry(
        ILogger logger,
        string dependency,
        string address)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TaskCanceledException>(ex => ex.InnerException is TimeoutException)
            .WaitAndRetryAsync(
                retryCount: 1,
                // Jitter matters because InterviewAPI runs four grading workers in parallel; a
                // fixed delay would line their retries up into a second synchronised burst.
                // Hand-rolled rather than Polly.Contrib.WaitAndRetry's DecorrelatedJitterBackoffV2:
                // that helper lives in a separate NuGet package, and one retry does not justify
                // adding a dependency this batch was not scoped to add.
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(200 + Random.Shared.Next(0, 200)),
                onRetry: (outcome, delay, attempt, _) => logger.LogWarning(
                    "Retrying {Dependency} at {Address} in {DelayMs}ms (attempt {Attempt}). Cause: {Failure}",
                    dependency,
                    address,
                    delay.TotalMilliseconds,
                    attempt,
                    DescribeFailure(outcome)));
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
