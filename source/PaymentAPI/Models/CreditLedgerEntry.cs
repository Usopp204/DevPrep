using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentAPI.Models;

[Table("CreditLedger")]
public class CreditLedgerEntry
{
    [Key]
    public Guid LedgerId { get; set; }
    public Guid UserId { get; set; }
    public string EntryType { get; set; } = "Purchase";
    public int CreditChange { get; set; }
    public int BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
