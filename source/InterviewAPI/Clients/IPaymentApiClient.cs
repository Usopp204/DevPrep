namespace InterviewAPI.Clients;

public interface IPaymentApiClient
{
    Task<CreditDeductionResponse> DeductCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct);

    Task<CreditDeductionResponse> RefundCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct);

    // One-time bonus award (e.g. enrolling both Face and Voice biometrics). referenceId is the
    // UserId itself (not a session) — PaymentAPI's own idempotency guard keys on it, so a caller
    // can call this every time the award condition looks true (re-enrollment, retries, ...)
    // without needing to track "did I already award this" locally.
    Task<CreditAwardResponse> AwardCreditAsync(
        Guid userId, int amount, Guid referenceId, string description, CancellationToken ct);

    // Returns a result, not a bare int, so callers can tell "the wallet is empty" apart from
    // "PaymentAPI did not answer". Returning 0 for both made an outage look identical to a user
    // who is out of credits — see CreditBalanceResult.
    Task<CreditBalanceResult> GetBalanceAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Three distinct states, which the previous `Task&lt;int&gt;` signature could only express as two:
///   Available = true,  Balance &gt; 0  -> the user has credits
///   Available = true,  Balance == 0 -> the user genuinely has none
///   Available = false                -> we do NOT know; PaymentAPI failed, timed out, or its
///                                       circuit breaker is open. Balance is meaningless here and
///                                       callers must not render it as a number.
/// The old code returned 0 on every failure, so an outage was indistinguishable from an empty
/// wallet and the UI confidently told users they had no credits while their money was fine.
/// </summary>
public class CreditBalanceResult
{
    public bool Available { get; set; }
    public int Balance { get; set; }
    public string? Message { get; set; }

    public static CreditBalanceResult Known(int balance) =>
        new() { Available = true, Balance = balance };

    public static CreditBalanceResult Unknown(string message) =>
        new() { Available = false, Message = message };
}

public class CreditDeductionResponse
{
    public bool Success { get; set; }
    public int BalanceAfter { get; set; }
    public int AmountDeducted { get; set; }
    public string? Message { get; set; }

    /// <summary>
    /// Whether PaymentAPI actually answered. Separates the two very different reasons Success can
    /// be false:
    ///   Success = false, ServiceAvailable = true  -> PaymentAPI answered and said no (e.g. the
    ///                                                user does not have enough credits). Message
    ///                                                is PaymentAPI's own wording and is meant for
    ///                                                the user.
    ///   Success = false, ServiceAvailable = false -> we never got an answer (host unreachable,
    ///                                                timeout, 5xx, open circuit). Message holds
    ///                                                transport detail such as
    ///                                                "Name or service not known (paymentapi:8080)"
    ///                                                and must NEVER reach the browser.
    /// Without this flag both collapsed into "Success = false" with a Message the caller could not
    /// classify, so the internal hostname was rendered to users during a PaymentAPI outage.
    /// </summary>
    public bool ServiceAvailable { get; set; } = true;
}

public class CreditAwardResponse
{
    public bool Success { get; set; }

    // True only when THIS call is what granted the bonus — false both on failure AND on the
    // idempotent no-op case (the user already had it). Callers that need to show a "you got a
    // bonus!" notification must check THIS field, not Success (Success is true for the no-op
    // case too — the call itself didn't fail, it just had nothing to do).
    public bool Awarded { get; set; }
    public int BalanceAfter { get; set; }
    public string? Message { get; set; }
}
