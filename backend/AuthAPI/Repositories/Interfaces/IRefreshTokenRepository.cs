using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);

    /// <summary>Lấy refresh token theo chuỗi token (kèm Account, có tracking để cập nhật).</summary>
    Task<RefreshToken?> GetByTokenAsync(string token);

    /// <summary>
    /// Thu hồi mọi refresh token đang hoạt động của một tài khoản (dùng khi đổi/đặt lại
    /// mật khẩu để buộc đăng nhập lại trên các thiết bị khác).
    /// </summary>
    Task RevokeAllActiveByAccountAsync(Guid accountId);
}
