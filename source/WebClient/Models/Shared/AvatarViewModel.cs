namespace WebClient.Models.Shared;

/// <summary>
/// Input for the shared _Avatar.cshtml fragment: render the uploaded picture, or the caller's
/// fallback character when there isn't one.
/// </summary>
public sealed class AvatarViewModel
{
    /// <summary>Absolute avatar URL, or null/blank when the user has never uploaded one.</summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Character shown when <see cref="AvatarUrl"/> is absent. Supplied by the caller and never
    /// derived here: the fallback is the caller's decision, not this fragment's.
    /// </summary>
    public string Initial { get; init; } = "U";
}
