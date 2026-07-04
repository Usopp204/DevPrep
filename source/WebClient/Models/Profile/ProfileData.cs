namespace WebClient.Models.Profile;

/// <summary>
/// Khớp ProfileResponse của UserAPI (email/fullName/avatarUrl) để giải mã data
/// trong BackendResponse&lt;ProfileData&gt;.
/// </summary>
public sealed class ProfileData
{
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
}
