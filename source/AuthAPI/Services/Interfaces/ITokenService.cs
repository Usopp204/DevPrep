using AuthAPI.Models;

namespace AuthAPI.Services.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Phát JWT access token cho tài khoản. Trả về chuỗi token và thời gian sống (giây).
    /// </summary>
    (string token, int expiresInSeconds) GenerateAccessToken(Account account);

    /// <summary>
    /// Sinh refresh token ngẫu nhiên an toàn về mặt mật mã học.
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>Số ngày hiệu lực của refresh token.</summary>
    int RefreshTokenDays { get; }
}
