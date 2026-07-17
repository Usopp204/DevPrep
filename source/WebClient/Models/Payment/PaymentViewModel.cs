using System.Text.Json.Serialization;

namespace WebClient.Models.Payment;

public class CreditPackageDto
{
    public Guid PackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Credits { get; set; }
    public int BonusCredits { get; set; }

    // DB không có cột TotalCredits — tính từ Credits + BonusCredits.
    // JsonIgnore để không map field từ JSON response (real API cũng không trả field này).
    [JsonIgnore]
    public int TotalCredits => Credits + BonusCredits;

    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
}

public class BuyCreditsViewModel
{
    public List<CreditPackageDto> Packages { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

public class CreateOrderRequest
{
    public Guid PackageId { get; set; }
}

public class CreateOrderResponse
{
    public string PaymentUrl { get; set; } = string.Empty;
}

public class PaymentSuccessViewModel
{
    public string OrderCode { get; set; } = "Unknown";
    public int Credits { get; set; }
    public string Amount { get; set; } = "0";
}

public class PaymentFailedViewModel
{
    public string Reason { get; set; } = "The payment was not completed.";
    public string? OrderCode { get; set; }
}

public class CreditBalanceDto
{
    public int Balance { get; set; }
    public int TotalPurchased { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
}

public class TransactionDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Credits { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = "VNPay";
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionHistoryViewModel
{
    public CreditBalanceDto Balance { get; set; } = new();
    public List<TransactionDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? ErrorMessage { get; set; }
}

/// <summary>1 dòng biến động credit (bảng CreditLedger của PaymentAPI).</summary>
public class CreditLedgerItemDto
{
    public Guid LedgerId { get; set; }
    public string EntryType { get; set; } = string.Empty;   // Usage / Purchase / Refund
    public int CreditChange { get; set; }                    // Usage âm, Purchase/Refund dương
    public int BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }                   // SessionId (Usage/Refund) hoặc OrderId (Purchase)
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Trang "Credit History" trong khu Profile — lịch sử trừ/cộng credit.</summary>
public class CreditHistoryViewModel
{
    public CreditBalanceDto Balance { get; set; } = new();
    public int TotalUsed { get; set; }          // tổng credit đã trừ (Usage, số dương)
    public int TotalRefunded { get; set; }
    public string? Type { get; set; }            // filter đang chọn (Usage/Purchase/Refund, null = tất cả)
    public List<CreditLedgerItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public string? ErrorMessage { get; set; }
}
