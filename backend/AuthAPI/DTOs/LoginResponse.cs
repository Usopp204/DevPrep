namespace AuthAPI.DTOs;

/// <summary>
/// Kết quả trả về sau khi đăng nhập thành công.
/// </summary>
public class LoginResponse
{
    public Guid AccountId { get; set; }

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string AccessToken { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;

    public string TokenType { get; set; } = "Bearer";

    /// <summary>Thời gian sống của access token (giây).</summary>
    public int ExpiresIn { get; set; }
}
