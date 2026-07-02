namespace WebClient.Models.Auth;

/// <summary>Phần "data" trong phản hồi đăng ký của AuthAPI (RegisterResponse).</summary>
public class RegisterResponseData
{
    public string? AccountId { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public bool IsEmailVerified { get; set; }
}
