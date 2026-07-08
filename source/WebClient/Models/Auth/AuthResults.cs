namespace WebClient.Models.Auth;

/// <summary>Kết quả một lời gọi auth đơn giản (thành công + thông báo).</summary>
public record AuthActionResult(bool Success, string Message);

/// <summary>Kết quả đăng nhập (kèm dữ liệu token khi thành công).</summary>
public record LoginResult(bool Success, string Message, LoginResponseData? Data);
