using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentAPI.Models;

[Table("PaymentTransactions")]
public class PaymentTransaction
{
    [Key]
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
