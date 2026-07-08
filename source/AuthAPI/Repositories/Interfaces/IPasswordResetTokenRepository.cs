using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);

    /// <summary>
    /// Lấy token còn hiệu lực (đúng token, chưa dùng, chưa hết hạn) kèm theo Account.
    /// </summary>
    Task<PasswordResetToken?> GetValidByTokenAsync(string token);

    /// <summary>
    /// Lấy tất cả token chưa sử dụng của một tài khoản (để vô hiệu hoá khi tạo token mới).
    /// </summary>
    Task<List<PasswordResetToken>> GetUnusedTokensByAccountAsync(Guid accountId);
}
