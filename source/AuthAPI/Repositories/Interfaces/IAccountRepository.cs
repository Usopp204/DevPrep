using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface IAccountRepository
{
    Task<bool> ExistsByEmailAsync(string email);

    Task<Account?> GetByEmailAsync(string email);

    Task<Account?> GetByIdAsync(Guid accountId);

    /// <summary>Lấy nhiều tài khoản theo danh sách Id (phục vụ resolve email cho service khác).</summary>
    Task<List<Account>> GetByIdsAsync(IReadOnlyCollection<Guid> accountIds);

    /// <summary>Tìm AccountId theo Email chứa keyword (phục vụ search theo email ở service khác).</summary>
    Task<List<Guid>> SearchAccountIdsByEmailAsync(string keyword);

    Task AddAsync(Account account);

    /// <summary>
    /// Lấy các account Pending chưa xác thực email và không còn token xác thực hợp lệ nào
    /// (đã quá hạn OTP/link) — ứng viên để job dọn dẹp xoá.
    /// </summary>
    Task<List<Account>> GetExpiredPendingAccountsAsync(int maxCount);

    /// <summary>
    /// Xoá cứng một account nếu và chỉ nếu nó VẪN đang Pending, chưa xác thực và hết token hợp lệ —
    /// điều kiện được áp lại ngay trong câu DELETE (nguyên tử) nên an toàn khi chạy đua với
    /// register/resend/sweeper khác. Trả về true nếu lượt gọi này xoá được; false nếu account
    /// đã bị bên khác xoá trước hoặc vừa được resend cứu sống. DB cascade tự xoá token con.
    /// </summary>
    Task<bool> TryDeleteExpiredPendingAsync(Guid accountId);

    /// <summary>
    /// Lưu mọi thay đổi đang được DbContext theo dõi (đóng vai trò unit-of-work).
    /// </summary>
    Task<int> SaveChangesAsync();
}
