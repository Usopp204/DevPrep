namespace WebClient.Models.Shared.TechStacks;

public sealed class TechStackListItemViewModel
{
    public int DisplayId { get; set; }
    public Guid TechStackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
