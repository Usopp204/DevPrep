namespace AuthAPI.DTOs;

/// <summary>
/// Trạng thái hiện tại của tài khoản đang đăng nhập — WebClient poll endpoint này (cache ngắn)
/// để tự đăng xuất ngay khi tài khoản bị block giữa phiên (access token vẫn còn hạn).
/// </summary>
public class AccountStatusResponse
{
    public string Status { get; set; } = string.Empty;   // Pending / Active / Blocked
    public string Role { get; set; } = string.Empty;     // User / Admin / Manager
}
