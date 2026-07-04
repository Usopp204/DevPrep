using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentAPI.Models;

[Table("Orders")]
public class Order
{
    [Key]
    public Guid OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid PackageId { get; set; }
    public decimal Amount { get; set; }
    public int Credits { get; set; }
    public string Currency { get; set; } = "VND";
    public string PaymentMethod { get; set; } = "VNPay";
    public string Status { get; set; } = "Pending";
    public DateTime? ExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
