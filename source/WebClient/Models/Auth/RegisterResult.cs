namespace WebClient.Models.Auth;

/// <summary>Kết quả gọi API đăng ký từ WebClient tới API Gateway.</summary>
public record RegisterResult(bool Success, string Message, string? Email);
