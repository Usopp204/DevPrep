using System.ComponentModel.DataAnnotations;

namespace AuthAPI.DTOs;

/// <summary>
/// Đăng nhập SSO qua Firebase (dùng chung cho Google lẫn GitHub).
/// FE đăng nhập bằng Firebase rồi gửi lên Firebase ID token (lấy từ user.getIdToken()).
/// Backend verify chữ ký token (khóa công khai của Google) rồi tự rút uid/email/name/provider.
/// </summary>
public class FirebaseLoginRequest
{
    [Required(ErrorMessage = "idToken is required.")]
    public string IdToken { get; set; } = null!;
}
