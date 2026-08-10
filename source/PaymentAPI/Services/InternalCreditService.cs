using Microsoft.Data.SqlClient;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace PaymentAPI.Services;

/// <summary>
/// Credit của user được suy ra từ CreditLedger: số dư = SUM(CreditChange). Bảng CreditWallets
/// đã bị bỏ, không còn dòng số dư riêng để khoá nữa.
///
/// Vì vậy mọi thao tác đọc-rồi-ghi ở đây mở transaction rồi đọc số dư qua
/// ILedgerRepository.GetBalanceForUpdateAsync — nó giữ khoá (UPDLOCK, HOLDLOCK) trên dải
/// WHERE UserId = @u tới hết transaction, nên lời gọi song song thứ hai phải đợi và sẽ đọc
/// được số dư mới. Đọc thuần bằng LINQ sẽ không chặn được gì, vì SUM không giữ lại khoá nào.
/// </summary>
public class InternalCreditService
{
    private readonly PaymentDbContext _db;
    private readonly ILedgerRepository _ledger;
    private readonly ILogger<InternalCreditService> _logger;

    public InternalCreditService(PaymentDbContext db, ILedgerRepository ledger,
                                 ILogger<InternalCreditService> logger)
    {
        _db = db;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task<InternalCreditDeductionResponse> DeductCreditAsync(
        InternalCreditDeductionRequest request, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var balance = await _ledger.GetBalanceForUpdateAsync(request.UserId, ct);

            if (balance < request.Amount)
                return new InternalCreditDeductionResponse
                {
                    Success = false,
                    Message = $"Insufficient credits. Required: {request.Amount}, Available: {balance}.",
                    BalanceAfter = balance
                };

            var balanceAfter = balance - request.Amount;

            // Giờ Việt Nam, khớp quy ước chung của codebase.
            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Usage",
                CreditChange = -request.Amount,
                BalanceAfter = balanceAfter,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            });

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new InternalCreditDeductionResponse
            {
                Success = true,
                BalanceAfter = balanceAfter,
                AmountDeducted = request.Amount,
                Message = "Credit deducted successfully."
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return new InternalCreditDeductionResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                BalanceAfter = 0
            };
        }
    }

    /// <summary>
    /// Hoàn credit cho MỘT lần trừ cụ thể, xác định bằng ReferenceId — chính là SessionId mà
    /// InterviewAPI truyền vào khi khởi tạo phiên phỏng vấn thất bại sau lúc đã trừ tiền.
    ///
    /// Bản trước cộng thẳng số tiền trong request vào sổ cái mà không kiểm tra gì: không cần có
    /// bút toán trừ tương ứng, không chặn gọi lặp, không giới hạn số tiền. Ai gọi được endpoint
    /// là nạp credit vô hạn. Ba lớp chặn dưới đây, theo thứ tự:
    ///   1. phải tồn tại bút toán Usage cùng (UserId, ReferenceId) — không có thì không có gì để hoàn;
    ///   2. số hoàn không vượt quá số đã trừ của chính lần đó;
    ///   3. mỗi ReferenceId chỉ hoàn được một lần — kiểm trong code cho thông báo rõ ràng, và
    ///      chốt lại ở tầng database bằng unique filtered index UX_Ledger_RefundPerReference
    ///      để bịt khe hở check-then-insert khi có hai lời gọi song song.
    ///
    /// LƯU Ý KHI TRIỂN KHAI: index UX_Ledger_RefundPerReference KHÔNG nằm trong repo (nhóm gửi
    /// script migration riêng, không theo dõi bằng Git — xem .gitignore). Máy nào chưa chạy script
    /// đó thì hai lời gọi hoàn song song cùng một ReferenceId vẫn có thể lọt qua, vì lúc ấy chỉ còn
    /// mỗi lớp kiểm trong code. Kiểm nhanh:
    ///     SELECT name FROM sys.indexes WHERE name = 'UX_Ledger_RefundPerReference';
    ///
    /// Admin cộng/trừ credit thủ công (Program.cs /admin/credits/*) ghi ReferenceId = null, nên
    /// vừa nằm ngoài bộ lọc của index, vừa không bao giờ bị nhận nhầm là "lần trừ để hoàn".
    /// </summary>
    public async Task<InternalCreditDeductionResponse> RefundCreditAsync(
        InternalCreditRefundRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0)
            return Rejected("Refund amount must be greater than zero.");

        if (request.ReferenceId == Guid.Empty)
            return Rejected("ReferenceId is required: a refund must point at the deduction it reverses.");

        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            // Khoá dải sổ cái của user tới hết transaction. Hai lời gọi hoàn song song buộc phải
            // xếp hàng, nên lời gọi thứ hai đọc được bút toán mà lời gọi thứ nhất vừa ghi.
            var balance = await _ledger.GetBalanceForUpdateAsync(request.UserId, ct);

            // (1) Phải có lần trừ tương ứng. Tổng CreditChange của các dòng Usage là số âm.
            var usageSum = await _db.CreditLedgerEntries
                .Where(e => e.UserId == request.UserId
                         && e.ReferenceId == request.ReferenceId
                         && e.EntryType == "Usage")
                .SumAsync(e => (int?)e.CreditChange, ct) ?? 0;
            var deducted = -usageSum;

            if (deducted <= 0)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(
                    "Từ chối hoàn credit: không có bút toán trừ nào cho reference {ReferenceId} của user {UserId}.",
                    request.ReferenceId, request.UserId);
                return Rejected($"No matching deduction for reference {request.ReferenceId}; nothing to refund.");
            }

            // (2) Đã hoàn rồi thì coi như không có việc gì làm, KHÔNG phải lỗi — InterviewAPI gọi
            // hàm này trong khối catch và có thể chạy lại, giống cách AwardCreditAsync xử lý.
            var alreadyRefunded = await _db.CreditLedgerEntries.AnyAsync(
                e => e.UserId == request.UserId
                  && e.ReferenceId == request.ReferenceId
                  && e.EntryType == "Refund", ct);

            if (alreadyRefunded)
            {
                await transaction.CommitAsync(ct);
                return new InternalCreditDeductionResponse
                {
                    Success = true,
                    BalanceAfter = balance,
                    AmountDeducted = 0,
                    Message = "Already refunded previously."
                };
            }

            // (3) Không hoàn quá số đã trừ.
            if (request.Amount > deducted)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(
                    "Từ chối hoàn credit: xin hoàn {Amount} nhưng reference {ReferenceId} chỉ bị trừ {Deducted}.",
                    request.Amount, request.ReferenceId, deducted);
                return Rejected($"Refund amount {request.Amount} exceeds the {deducted} credits deducted for this reference.");
            }

            var balanceAfter = balance + request.Amount;

            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Refund",
                CreditChange = request.Amount,
                BalanceAfter = balanceAfter,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            });

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Thua cuộc đua với một lời gọi hoàn song song cho cùng reference; index đã chặn
                // ở phía server. Kết quả giống nhánh alreadyRefunded ở trên: đã hoàn, không lỗi.
                await transaction.RollbackAsync(ct);
                _logger.LogInformation(
                    "RefundCreditAsync: reference {ReferenceId} đã được hoàn bởi một lời gọi song song.",
                    request.ReferenceId);
                return new InternalCreditDeductionResponse
                {
                    Success = true,
                    BalanceAfter = await _ledger.GetBalanceByUserAsync(request.UserId),
                    AmountDeducted = 0,
                    Message = "Already refunded previously."
                };
            }

            await transaction.CommitAsync(ct);

            return new InternalCreditDeductionResponse
            {
                Success = true,
                BalanceAfter = balanceAfter,
                AmountDeducted = request.Amount,
                Message = "Credit refunded successfully."
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi hoàn credit cho user {UserId}, reference {ReferenceId}.",
                request.UserId, request.ReferenceId);
            return new InternalCreditDeductionResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                BalanceAfter = 0
            };
        }
    }

    private static InternalCreditDeductionResponse Rejected(string message) => new()
    {
        Success = false,
        Message = message,
        BalanceAfter = 0,
        AmountDeducted = 0
    };

    // One-time bonus (100 credits for enrolling both Face and Voice biometrics). Idempotent —
    // guarded both by an existence check and a unique filtered index on CreditLedger
    // (UX_Ledger_UserBonus) to close the check-then-insert race under concurrent calls.
    public async Task<InternalCreditAwardResponse> AwardCreditAsync(
        InternalCreditAwardRequest request, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var balance = await _ledger.GetBalanceForUpdateAsync(request.UserId, ct);

            // Fast-path check — avoids a pointless insert attempt on the common repeat call. The
            // unique filtered index below is the guard that actually matters under concurrency;
            // this alone would have the same TOCTOU race as any check-then-insert.
            var alreadyAwarded = await _db.CreditLedgerEntries.AnyAsync(
                e => e.UserId == request.UserId && e.EntryType == "Bonus", ct);
            if (alreadyAwarded)
            {
                await transaction.CommitAsync(ct);
                return new InternalCreditAwardResponse
                {
                    Success = true,
                    Awarded = false,
                    BalanceAfter = balance,
                    Message = "Bonus already awarded previously."
                };
            }

            var balanceAfter = balance + request.Amount;

            _db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Bonus",
                CreditChange = request.Amount,
                BalanceAfter = balanceAfter,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow.AddHours(7)
            });

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Lost a race against a concurrent award for the same user — the unique filtered
                // index caught it server-side. Same outcome as the fast-path check above:
                // already awarded, not an error.
                await transaction.RollbackAsync(ct);
                _logger.LogInformation(
                    "AwardCreditAsync: bonus đã được cộng bởi một lời gọi song song, userId={UserId}.",
                    request.UserId);
                // Đọc thuần: transaction đã rollback nên không còn gì để khoá, và ở đây chỉ cần
                // một con số để trả về cho người gọi.
                return new InternalCreditAwardResponse
                {
                    Success = true,
                    Awarded = false,
                    BalanceAfter = await _ledger.GetBalanceByUserAsync(request.UserId),
                    Message = "Bonus already awarded previously."
                };
            }

            await transaction.CommitAsync(ct);

            return new InternalCreditAwardResponse
            {
                Success = true,
                Awarded = true,
                BalanceAfter = balanceAfter,
                Message = "Credit bonus awarded successfully."
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return new InternalCreditAwardResponse
            {
                Success = false,
                Awarded = false,
                Message = $"Error: {ex.Message}",
                BalanceAfter = 0
            };
        }
    }

    // SQL Server unique-constraint/index violation (2601 = duplicate on unique index, 2627 =
    // duplicate on unique constraint) — either can surface here depending on how the index was
    // created. Dùng chung cho cả guard Bonus (UX_Ledger_UserBonus) lẫn guard Refund
    // (UX_Ledger_RefundPerReference).
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
}
