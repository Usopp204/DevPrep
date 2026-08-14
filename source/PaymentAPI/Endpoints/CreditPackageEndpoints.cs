using System.Text.Json;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Endpoints;

/// <summary>
/// Credit package routes: the catalogue a buyer sees, and the Admin screens that maintain it.
/// </summary>
/// <remarks>
/// Moved out of Program.cs, which had grown to 746 lines holding nineteen business endpoints.
/// Program.cs is where the application is configured and started; a route body deciding whether
/// a package name is already taken is not configuration. CvJdAPI already splits its routes this
/// way (CvEndpoints / JdEndpoints / MatchEndpoints) and its Program.cs is 73 lines, so this
/// follows a pattern the codebase already had rather than inventing one.
///
/// Deleting a package is a soft delete — IsActive goes false and the row stays, because Orders
/// reference PackageId and a hard delete would orphan every past purchase of that package.
/// That is also why restore exists as its own route.
/// </remarks>
public static class CreditPackageEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static void MapCreditPackageEndpoints(this WebApplication app)
    {
        // Buyer-facing: only packages currently on sale.
        app.MapGet("/packages", async (ICreditPackageRepository repo) =>
            Results.Ok(await repo.GetActiveAsync()));

        // Admin sees inactive ones too, so a package can be found and restored after deletion.
        app.MapGet("/admin/credit-packages", async (ICreditPackageRepository repo) =>
        {
            var packages = await repo.GetAllAsync();
            return Results.Ok(packages.Cast<CreditPackage>().OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt));
        }).RequireAuthorization("AdminArea");

        app.MapGet("/admin/credit-packages/{id:guid}", async (Guid id, ICreditPackageRepository repo) =>
        {
            var pkg = await repo.GetByIdAsync(id);
            return pkg is null ? Results.NotFound() : Results.Ok(pkg);
        }).RequireAuthorization("AdminArea");

        app.MapPost("/admin/credit-packages", async (HttpContext ctx, ICreditPackageRepository repo) =>
        {
            AdminPackageRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<AdminPackageRequest>(ctx.Request.Body, JsonOpts); }
            catch { return Results.BadRequest("Invalid request body."); }
            if (body is null) return Results.BadRequest();

            // Uniqueness is checked among ACTIVE packages only: a name freed by a soft delete
            // can be reused, which would be impossible if deleted rows also blocked it.
            if (await repo.ExistsActiveNameAsync(body.Name))
                return Results.Conflict($"Active package with name '{body.Name}' already exists.");

            var now = DateTime.UtcNow.AddHours(7);
            var pkg = new CreditPackage
            {
                PackageId    = Guid.NewGuid(),
                Name         = body.Name,
                Description  = body.Description,
                Credits      = body.Credits,
                BonusCredits = body.BonusCredits,
                Price        = body.Price,
                Currency     = body.Currency ?? "VND",
                IsActive     = true,
                SortOrder    = body.SortOrder,
                CreatedAt    = now,
                UpdatedAt    = now
            };
            await repo.AddAsync(pkg);
            await repo.SaveChangesAsync();
            return Results.Created($"/admin/credit-packages/{pkg.PackageId}", pkg);
        }).RequireAuthorization("AdminArea");

        app.MapPut("/admin/credit-packages/{id:guid}", async (Guid id, HttpContext ctx, ICreditPackageRepository repo) =>
        {
            var pkg = await repo.GetByIdAsync(id);
            if (pkg is null) return Results.NotFound();

            AdminPackageRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<AdminPackageRequest>(ctx.Request.Body, JsonOpts); }
            catch { return Results.BadRequest("Invalid request body."); }
            if (body is null) return Results.BadRequest();

            // Excludes this row from the check, otherwise saving a package without renaming it
            // would collide with itself.
            if (await repo.ExistsActiveNameAsync(body.Name, id))
                return Results.Conflict($"Another active package named '{body.Name}' already exists.");

            pkg.Name         = body.Name;
            pkg.Description  = body.Description;
            pkg.Credits      = body.Credits;
            pkg.BonusCredits = body.BonusCredits;
            pkg.Price        = body.Price;
            pkg.Currency     = body.Currency ?? pkg.Currency;
            pkg.SortOrder    = body.SortOrder;
            pkg.UpdatedAt    = DateTime.UtcNow.AddHours(7);
            await repo.SaveChangesAsync();
            return Results.Ok(pkg);
        }).RequireAuthorization("AdminArea");

        app.MapDelete("/admin/credit-packages/{id:guid}", async (Guid id, ICreditPackageRepository repo) =>
        {
            var pkg = await repo.GetByIdAsync(id);
            if (pkg is null) return Results.NotFound();

            pkg.IsActive  = false;
            pkg.UpdatedAt = DateTime.UtcNow.AddHours(7);
            await repo.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization("AdminArea");

        app.MapPost("/admin/credit-packages/{id:guid}/restore", async (Guid id, ICreditPackageRepository repo) =>
        {
            var pkg = await repo.GetByIdAsync(id);
            if (pkg is null) return Results.NotFound();
            if (pkg.IsActive) return Results.Ok(pkg);

            pkg.IsActive  = true;
            pkg.UpdatedAt = DateTime.UtcNow.AddHours(7);
            await repo.SaveChangesAsync();
            return Results.Ok(pkg);
        }).RequireAuthorization("AdminArea");
    }
}

public record AdminPackageRequest(
    string Name,
    string? Description,
    int Credits,
    int BonusCredits,
    decimal Price,
    string? Currency,
    int SortOrder,
    bool IsActive = true
);
