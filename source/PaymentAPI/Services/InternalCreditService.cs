using Microsoft.Data.SqlClient;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace PaymentAPI.Services;

public class InternalCreditService
{
    private readonly PaymentDbContext _db;
    private readonly ILogger<InternalCreditService> _logger;

    public InternalCreditService(PaymentDbContext db, ILogger<InternalCreditService> logger)
    {
        _db = db;
        _logger = logger;
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
        InternalCreditAwardRequest request, CancellationToken ct, int retryCount = 0)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var wallet = await _db.CreditWallets.FirstOrDefaultAsync(
                w => w.UserId == request.UserId, ct);

            // Wallets were previously only ever created via a completed VNPay top-up (see
            // VnPayService.cs's IPN callback). A user who enrolls biometrics before ever
            // buying credits has no wallet row at all, so the bonus could never be awarded.
            // Create a zero-balance wallet here instead of rejecting the award.
            var walletJustCreated = false;
            if (wallet is null)
            {
                wallet = new CreditWallet
                {
                    WalletId = Guid.NewGuid(),
                    UserId = request.UserId,
                    Balance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.CreditWallets.Add(wallet);

                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueBonusViolation(ex))
                {
                    // Lost a race against a concurrent AwardCreditAsync call that created the
                    // SAME user's wallet first. This only fires once
                    // Sql/002_add_credit_wallets_userid_unique_index.sql has actually been
                    // applied — without that index there is nothing to violate, so duplicate
                    // wallet rows are still possible until it's run. Unlike the ledger-bonus
                    // race handled below, losing this race does NOT mean the bonus was already
                    // awarded — it only means someone else's wallet insert won, so retry the
                    // whole call: it will re-read the wallet the winner just created and award
                    // normally instead of failing.
                    await transaction.RollbackAsync(ct);
                    _db.Entry(wallet).State = EntityState.Detached;

                    // Hard cap on the retry: recovering here assumes the wallet row that just
                    // collided with ours is actually found when we re-read it, which holds today
                    // (nothing in PaymentAPI deletes CreditWallets rows) but is not guaranteed by
                    // any structural constraint — it's an assumption about this codebase's current
                    // behavior, not an enforced invariant. If that assumption is ever broken,
                    // retrying unconditionally would recurse forever and crash the process. Cap it
                    // at one retry and fail with a normal error response instead.
                    if (retryCount >= 1)
                    {
                        _logger.LogWarning(
                            "AwardCreditAsync hit the wallet-creation race retry limit for " +
                            "userId={UserId} — the wallet row expected after a unique-index " +
                            "violation was not found on retry.",
                            request.UserId);
                        return new InternalCreditAwardResponse
                        {
                            Success = false,
                            Awarded = false,
                            Message = "Could not process the award due to a concurrent wallet update. Please try again.",
                            BalanceAfter = 0
                        };
                    }

                    return await AwardCreditAsync(request, ct, retryCount + 1);
                }

                walletJustCreated = true;
            }

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
            // Skip Update() when the wallet was just Add()-ed above: calling Update() on an
            // entity already tracked as Added flips it to Modified, which would generate an
            // UPDATE matching zero rows instead of the pending INSERT — silently losing the
            // new wallet row while still reporting success.
            if (!walletJustCreated)
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