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

    Task<int> GetBalanceAsync(Guid userId, CancellationToken ct);
}

public class CreditDeductionResponse
{
    public bool Success { get; set; }
    public int BalanceAfter { get; set; }
    public int AmountDeducted { get; set; }
    public string? Message { get; set; }
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
