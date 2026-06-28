namespace AuthAPI.Services.Interfaces;

public interface IGitHubOAuthClient
{
    /// <summary>
    /// Đổi authorization code lấy access token rồi lấy thông tin người dùng GitHub.
    /// Ném lỗi nếu code không hợp lệ.
    /// </summary>
    Task<ExternalUserInfo> GetUserAsync(string code, string? redirectUri);
}
