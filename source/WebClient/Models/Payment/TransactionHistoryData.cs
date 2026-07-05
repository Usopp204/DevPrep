using WebClient.Models.Shared;

namespace WebClient.Models.Payment;

/// <summary>
/// Khớp TransactionHistoryResultDto của PaymentAPI để giải mã data
/// trong BackendResponse&lt;TransactionHistoryData&gt;.
/// </summary>
public sealed class TransactionHistoryData
{
    public WalletBalanceData Balance { get; set; } = new();
    public PagedResult<TransactionData> Transactions { get; set; } = new();
}

public sealed class WalletBalanceData
{
    public int Balance { get; set; }
    public int TotalPurchased { get; set; }
}

public sealed class TransactionData
{
    public string OrderCode { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
