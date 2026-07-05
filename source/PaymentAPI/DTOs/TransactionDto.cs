namespace PaymentAPI.DTOs;

public class TransactionDto
{
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
