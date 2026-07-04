namespace PaymentAPI.DTOs;

public class CreditPackageDto
{
    public Guid PackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Credits { get; set; }
    public int BonusCredits { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
