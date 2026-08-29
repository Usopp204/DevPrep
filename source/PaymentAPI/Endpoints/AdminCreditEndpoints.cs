using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;

namespace PaymentAPI.Endpoints;

public static class AdminCreditEndpoints
{
    public static void MapAdminCreditEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/credits/add", AddAsync).RequireAuthorization("AdminArea");
        app.MapPost("/admin/credits/deduct", DeductAsync).RequireAuthorization("AdminArea");
    }

    public static Task<IResult> AddAsync(AdminCreditAdjustRequest req, IAuthAccountClient authClient,
        PaymentDbContext db, ILedgerRepository ledgerRepo, CancellationToken ct) =>
        AdjustAsync(req, authClient, db, ledgerRepo, false, ct);

    public static Task<IResult> DeductAsync(AdminCreditAdjustRequest req, IAuthAccountClient authClient,
        PaymentDbContext db, ILedgerRepository ledgerRepo, CancellationToken ct) =>
        AdjustAsync(req, authClient, db, ledgerRepo, true, ct);

    private static async Task<IResult> AdjustAsync(AdminCreditAdjustRequest req, IAuthAccountClient authClient,
        PaymentDbContext db, ILedgerRepository ledgerRepo, bool deduct, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return Results.BadRequest(new { Success = false, Message = "Email is required." });
        if (req.Amount < 1 || req.Amount > 1_000_000)
            return Results.BadRequest(new { Success = false, Message = "Amount must be between 1 and 1,000,000." });

        ResolvedAccount? account;
        try { account = await authClient.FindByExactEmailAsync(req.Email, ct); }
        catch { return Results.Json(new { Success = false, Message = "Cannot reach AuthAPI to resolve the email." }, statusCode: 502); }
        if (account is null)
            return Results.NotFound(new { Success = false, Message = "No account found with this email." });

        IDbContextTransaction? transaction = null;
        try
        {
            if (db.Database.IsRelational()) transaction = await db.Database.BeginTransactionAsync(ct);
            var balance = await ledgerRepo.GetBalanceForUpdateAsync(account.AccountId, ct);
            if (deduct && balance < req.Amount)
                return Results.BadRequest(new { Success = false, Message = $"Insufficient credits. Available: {balance}, requested: {req.Amount}." });

            var change = deduct ? -req.Amount : req.Amount;
            var balanceAfter = balance + change;
            db.CreditLedgerEntries.Add(new CreditLedgerEntry
            {
                LedgerId = Guid.NewGuid(), UserId = account.AccountId,
                EntryType = deduct ? "Usage" : "Refund", CreditChange = change,
                BalanceAfter = balanceAfter, ReferenceId = null,
                Description = string.IsNullOrWhiteSpace(req.Description)
                    ? deduct ? "Admin credit deduction" : "Admin credit adjustment"
                    : req.Description.Trim(),
                CreatedAt = DateTime.UtcNow.AddHours(7)
            });
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return Results.Ok(new { Success = true, account.Email, AccountId = account.AccountId, BalanceAfter = balanceAfter });
        }
        catch (Exception ex)
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            return Results.Json(new { Success = false, Message = $"Error: {ex.Message}" }, statusCode: 500);
        }
        finally { if (transaction is not null) await transaction.DisposeAsync(); }
    }
}

public record AdminCreditAdjustRequest(string Email, int Amount, string? Description);
