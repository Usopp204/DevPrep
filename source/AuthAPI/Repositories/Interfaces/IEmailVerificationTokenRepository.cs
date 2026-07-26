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

    /// <summary>
    /// Tìm token còn hiệu lực theo giá trị token (kèm Account, tracked) — phục vụ xác thực qua link
    /// khi request không có email. Chỉ dùng cho token link dài, không dùng cho OTP 6 số.
    /// </summary>
    Task<EmailVerificationToken?> GetValidByTokenValueAsync(string token);

    /// <summary>
    /// Tài khoản còn ít nhất một token xác thực hợp lệ (chưa dùng, chưa hết hạn) hay không.
    /// Dùng làm "đồng hồ sống" của account Pending: hết token hợp lệ = hết hạn đăng ký.
    /// </summary>
    Task<bool> HasValidTokenAsync(Guid accountId);
}
