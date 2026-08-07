using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories;

public class LedgerRepository : Repository<CreditLedgerEntry>, ILedgerRepository
{
    public LedgerRepository(PaymentDbContext db) : base(db) { }

    // Đọc thuần, không khoá — dùng cho màn hình chỉ hiển thị.
    // int? rồi ?? 0: SumAsync trên tập rỗng trả về null chứ không phải 0.
    public async Task<int> GetBalanceByUserAsync(Guid userId)
        => await _set.Where(e => e.UserId == userId)
                     .SumAsync(e => (int?)e.CreditChange) ?? 0;

    // Đọc để chuẩn bị ghi. BẮT BUỘC gọi trong transaction.
    //
    // UPDLOCK: hai lời gọi song song cho cùng một user không thể cùng giữ khoá — lời gọi thứ hai
    // đợi lời gọi thứ nhất commit rồi mới đọc, nên nó thấy số dư mới.
    // HOLDLOCK: giữ khoá dải tới hết transaction, chặn dòng mới chen vào giữa lúc đọc và lúc ghi.
    //
    // Vì sao không dùng transaction SERIALIZABLE với LINQ thuần: cả hai phiên cùng lấy khoá chia
    // sẻ khi SUM, rồi cùng cần nâng lên khoá độc quyền để INSERT — kẹt nhau và SQL Server giết
    // một phiên (Msg 1205). Đã kiểm chứng bằng hai phiên chạy song song. UPDLOCK không cho hai
    // phiên cùng đọc ngay từ đầu nên không có vòng kẹt.
    public async Task<int> GetBalanceForUpdateAsync(Guid userId, CancellationToken ct = default)
        => await _db.Database
                    .SqlQuery<int>($@"
                        SELECT ISNULL(SUM(CreditChange), 0) AS Value
                        FROM dbo.CreditLedger WITH (UPDLOCK, HOLDLOCK)
                        WHERE UserId = {userId}")
                    .SingleAsync(ct);

    public async Task<DateTime?> GetLastChangedAtByUserAsync(Guid userId)
        => await _set.Where(e => e.UserId == userId)
                     .MaxAsync(e => (DateTime?)e.CreatedAt);

    public async Task<bool> HasPurchaseForOrderAsync(Guid orderId)
        => await _set.AnyAsync(e => e.EntryType == "Purchase" && e.ReferenceId == orderId);
}
