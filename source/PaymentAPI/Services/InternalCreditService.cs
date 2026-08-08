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

    public async Task<InternalCreditDeductionResponse> RefundCreditAsync(
        InternalCreditRefundRequest request, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var balanceAfter = await _ledger.GetBalanceForUpdateAsync(request.UserId, ct) + request.Amount;

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

            await _db.SaveChangesAsync(ct);
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
            return new InternalCreditDeductionResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                BalanceAfter = 0
            };
        }
    }

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
            catch (DbUpdateException ex) when (IsUniqueBonusViolation(ex))
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
    // created.
    private static bool IsUniqueBonusViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
}
