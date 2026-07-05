namespace WebClient.Models.Payment;

public class TransactionHistoryViewModel
{
    public WalletBalanceViewModel Balance { get; set; } = new();
    public List<TransactionItemViewModel> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public string? ErrorMessage { get; set; }
}

public class WalletBalanceViewModel
{
    public int Balance { get; set; }
    public int TotalPurchased { get; set; }
}

public class TransactionItemViewModel
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
