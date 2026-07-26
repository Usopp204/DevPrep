using Microsoft.Data.SqlClient;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace PaymentAPI.Services;

public class InternalCreditService
{
    private readonly PaymentDbContext _db;

    public InternalCreditService(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<InternalCreditDeductionResponse> DeductCreditAsync(
        InternalCreditDeductionRequest request, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var wallet = await _db.CreditWallets.FirstOrDefaultAsync(
                w => w.UserId == request.UserId, ct);

            if (wallet is null)
                return new InternalCreditDeductionResponse
                {
                    Success = false,
                    Message = "User wallet not found.",
                    BalanceAfter = 0
                };

            if (wallet.Balance < request.Amount)
                return new InternalCreditDeductionResponse
                {
                    Success = false,
                    Message = $"Insufficient credits. Required: {request.Amount}, Available: {wallet.Balance}.",
                    BalanceAfter = wallet.Balance
                };

            // Deduct credit
            wallet.Balance -= request.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _db.CreditWallets.Update(wallet);

            // Record ledger entry
            var ledger = new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Usage",
                CreditChange = -request.Amount,
                BalanceAfter = wallet.Balance,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };
            _db.CreditLedgerEntries.Add(ledger);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new InternalCreditDeductionResponse
            {
                Success = true,
                BalanceAfter = wallet.Balance,
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
            var wallet = await _db.CreditWallets.FirstOrDefaultAsync(
                w => w.UserId == request.UserId, ct);

            if (wallet is null)
                return new InternalCreditDeductionResponse
                {
                    Success = false,
                    Message = "User wallet not found.",
                    BalanceAfter = 0
                };

            // Refund credit
            wallet.Balance += request.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _db.CreditWallets.Update(wallet);

            // Record ledger entry
            var ledger = new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Refund",
                CreditChange = request.Amount,
                BalanceAfter = wallet.Balance,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };
            _db.CreditLedgerEntries.Add(ledger);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new InternalCreditDeductionResponse
            {
                Success = true,
                BalanceAfter = wallet.Balance,
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
            var wallet = await _db.CreditWallets.FirstOrDefaultAsync(
                w => w.UserId == request.UserId, ct);

            if (wallet is null)
                return new InternalCreditAwardResponse
                {
                    Success = false,
                    Awarded = false,
                    Message = "User wallet not found.",
                    BalanceAfter = 0
                };

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
                    BalanceAfter = wallet.Balance,
                    Message = "Bonus already awarded previously."
                };
            }

            wallet.Balance += request.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            _db.CreditWallets.Update(wallet);

            var ledger = new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(),
                UserId = request.UserId,
                EntryType = "Bonus",
                CreditChange = request.Amount,
                BalanceAfter = wallet.Balance,
                ReferenceId = request.ReferenceId,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };
            _db.CreditLedgerEntries.Add(ledger);

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
                var current = await _db.CreditWallets.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.UserId == request.UserId, ct);
                return new InternalCreditAwardResponse
                {
                    Success = true,
                    Awarded = false,
                    BalanceAfter = current?.Balance ?? wallet.Balance,
                    Message = "Bonus already awarded previously."
                };
            }

            await transaction.CommitAsync(ct);

            return new InternalCreditAwardResponse
            {
                Success = true,
                Awarded = true,
                BalanceAfter = wallet.Balance,
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