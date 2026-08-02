namespace WebClient.Models.Shared;

public sealed class StarRatingViewModel
{
    public string Name { get; init; } = string.Empty;
    public string InstancePrefix { get; init; } = string.Empty;
    public string Label { get; init; } = "Rating";
    public int? Value { get; init; }
    public bool Editable { get; init; }
    public bool Required { get; init; }
    public bool Disabled { get; init; }
    public bool Compact { get; init; }
    public bool ShowNumericValue { get; init; } = true;
    public string? DescribedBy { get; init; }
    public string? CssClass { get; init; }
}
