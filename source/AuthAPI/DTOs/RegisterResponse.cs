namespace AuthAPI.DTOs;

/// <summary>
/// Dữ liệu trả về cho client sau khi đăng ký thành công.
/// </summary>
public class RegisterResponse
{
    public Guid AccountId { get; set; }

    public string Email { get; set; } = null!;

    public string Status { get; set; } = null!;

    public bool IsEmailVerified { get; set; }
}
