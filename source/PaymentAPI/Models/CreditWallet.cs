using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentAPI.Models;

[Table("CreditWallets")]
public class CreditWallet
{
    [Key]
    public Guid WalletId { get; set; }

    public Guid UserId { get; set; }

    public int Balance { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
