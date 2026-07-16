namespace InterviewAPI.Clients;

public interface IPaymentApiClient
{
    Task<CreditDeductionResponse> DeductCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct);

    Task<CreditDeductionResponse> RefundCreditAsync(
        Guid userId, int amount, Guid sessionId, string description, CancellationToken ct);

    Task<int> GetBalanceAsync(Guid userId, CancellationToken ct);
}

public class CreditDeductionResponse
{
    public bool Success { get; set; }
    public int BalanceAfter { get; set; }
    public int AmountDeducted { get; set; }
    public string? Message { get; set; }
}
