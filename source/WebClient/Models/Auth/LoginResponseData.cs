namespace WebClient.Models.Auth;

/// <summary>Phần "data" của phản hồi đăng nhập AuthAPI (LoginResponse).</summary>
public class LoginResponseData
{
    public string? AccountId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
}
