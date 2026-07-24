using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Yêu cầu xác thực email bằng token trong link (người dùng bấm nút trong email,
/// không cần nhập email/OTP). Token là chuỗi base64url 32 byte sinh lúc đăng ký/resend.
/// </summary>
public class VerifyEmailLinkRequest
{
    [Required]
    [StringLength(256)]
    public string Token { get; set; } = null!;
}
