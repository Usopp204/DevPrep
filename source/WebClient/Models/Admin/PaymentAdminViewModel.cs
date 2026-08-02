namespace WebClient.Models.Admin;

public class CreditPackageAdminDto
{
    public Guid PackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Credits { get; set; }
    public int BonusCredits { get; set; }
    public int TotalCredits => Credits + BonusCredits;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreditPackagesAdminViewModel
{
    public List<CreditPackageAdminDto> Packages { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class AdminPaymentOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public decimal Amount { get; set; }
    public int Credits { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminPaymentsViewModel
{
    public List<AdminPaymentOrderDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PaidCount { get; set; }
    public string? ErrorMessage { get; set; }

    // Bộ lọc đang áp dụng — dùng để render lại form và giữ query string khi phân trang.
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public RevenueReportViewModel? Revenue { get; set; }
}

/// <summary>
/// Shape of GET /api/payment/admin/payments/{id}: the order plus its VNPay callback record.
/// Transaction stays null for orders that were never paid (no callback was ever stored).
/// </summary>
public class AdminPaymentDetailDto
{
    public AdminOrderDetailDto? Order { get; set; }
    public AdminTransactionDetailDto? Transaction { get; set; }
}

public class AdminOrderDetailDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }
    public decimal Amount { get; set; }
    public int Credits { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AdminTransactionDetailDto
{
    public Guid TransactionId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpResponseCode { get; set; }
    public string? VnpPayDate { get; set; }
    public bool? SecureHashValid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RawCallbackJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RevenueReportViewModel
{
    public string Period { get; set; } = "today";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int PackagesSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalCredits { get; set; }
    public string? ErrorMessage { get; set; }
}
