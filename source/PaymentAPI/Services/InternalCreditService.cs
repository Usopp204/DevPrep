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
}
