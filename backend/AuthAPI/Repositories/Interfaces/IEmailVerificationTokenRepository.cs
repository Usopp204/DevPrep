using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token);

    /// <summary>
    /// Lấy token còn hiệu lực (đúng mã, chưa dùng, chưa hết hạn) của một tài khoản.
    /// </summary>
    Task<EmailVerificationToken?> GetValidTokenAsync(Guid accountId, string token);

    /// <summary>
    /// Lấy tất cả token chưa sử dụng của một tài khoản (để vô hiệu hoá khi gửi lại OTP).
    /// </summary>
    Task<List<EmailVerificationToken>> GetUnusedTokensByAccountAsync(Guid accountId);
}
