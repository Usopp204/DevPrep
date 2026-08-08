using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public interface ILedgerRepository : IRepository<CreditLedgerEntry>
{
    /// <summary>
    /// Số dư credit của user = tổng CreditChange trên toàn bộ sổ cái. Thay cho
    /// CreditWallets.Balance sau khi bảng đó bị bỏ. Đọc thuần, không khoá.
    /// </summary>
    Task<int> GetBalanceByUserAsync(Guid userId);

    /// <summary>
    /// Như trên nhưng giữ khoá (UPDLOCK, HOLDLOCK) tới hết transaction. Dùng cho mọi thao tác
    /// đọc-số-dư-rồi-ghi. Phải gọi bên trong một transaction, nếu không khoá nhả ngay lập tức
    /// và không bảo vệ được gì.
    /// </summary>
    Task<int> GetBalanceForUpdateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Thời điểm biến động credit gần nhất, null nếu user chưa có giao dịch nào.</summary>
    Task<DateTime?> GetLastChangedAtByUserAsync(Guid userId);

    /// <summary>
    /// Đơn hàng này đã được cộng credit chưa. VNPay báo kết quả qua HAI đường độc lập
    /// (trình duyệt quay về và IPN server-to-server) nên cả hai đều phải hỏi câu này trước
    /// khi cộng, nếu không user được cộng hai lần hoặc không lần nào.
    /// </summary>
    Task<bool> HasPurchaseForOrderAsync(Guid orderId);
}
