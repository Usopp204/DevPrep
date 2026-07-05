namespace PaymentAPI.DTOs;

public class ManagerPaymentQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
}

public class ManagerPaymentListResultDto
{
    public List<ManagerPaymentListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PaidCount { get; set; }
}

public class ManagerPaymentListItemDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }
    public decimal Amount { get; set; }
    public int Credits { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = "VNPay";
    public string Status { get; set; } = "Pending";
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpResponseCode { get; set; }
    public string? TransactionStatus { get; set; }
}

public class ManagerPaymentDetailDto : ManagerPaymentListItemDto
{
    public DateTime? ExpiredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ManagerPaymentTransactionDto? Transaction { get; set; }
}

public class ManagerPaymentTransactionDto
{
    public Guid TransactionId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpResponseCode { get; set; }
    public string? VnpPayDate { get; set; }
    public bool? SecureHashValid { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RawCallbackJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
