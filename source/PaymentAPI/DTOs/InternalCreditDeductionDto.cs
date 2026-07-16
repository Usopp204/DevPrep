namespace PaymentAPI.DTOs;

public class InternalCreditDeductionRequest
{
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public Guid ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class InternalCreditDeductionResponse
{
    public bool Success { get; set; }
    public int BalanceAfter { get; set; }
    public int AmountDeducted { get; set; }
    public string? Message { get; set; }
}

public class InternalCreditRefundRequest
{
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public Guid ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
}
