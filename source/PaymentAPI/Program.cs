using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

try { Env.TraversePath().Load(".env.example"); } catch { }
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("PAYMENT_API_PORT", 7008);
builder.WebHost.UseUrls($"http://0.0.0.0:{servicePort}");

// ── Connection String: CHỈ đọc từ .env (key PAYMENT_DB_CONNECTION_STRING) ────
var connectionString =
    builder.Configuration.GetValue<string>("PAYMENT_DB_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("PAYMENT_DB_CONNECTION_STRING is missing in .env.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(connectionString));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey    = builder.Configuration["Jwt:Key"]      ?? throw new InvalidOperationException("Jwt:Key is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]   ?? "DevPrep.AuthAPI";
var jwtAud    = builder.Configuration["Jwt:Audience"] ?? "DevPrep.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAud,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType            = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICreditPackageRepository, CreditPackageRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICreditPackageService, CreditPackageService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<InternalCreditService>();

// ── AuthAPI internal client (resolve email -> AccountId cho admin adjust credit) ──
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
var authBaseUrl = builder.Configuration.GetValue<string>("AUTH_API_BASEURL", "http://localhost:7001");
builder.Services.AddHttpClient<IAuthAccountClient, AuthAccountClient>(client =>
{
    client.BaseAddress = new Uri(authBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

// ── Controllers & Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentAPI", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter JWT token (without 'Bearer ' prefix)",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// --- Health ---
app.MapGet("/health", () => Results.Ok(new { service = "Payment", status = "ok" }))
    .WithName("GetHealth")
    .WithOpenApi();

// --- Packages ---
app.MapGet("/packages", async (ICreditPackageRepository repo) =>
    Results.Ok(await repo.GetActiveAsync()));

// --- POST /vnpay/order ---
app.MapPost("/vnpay/order", async (HttpContext ctx, IVnPayService vnPay) =>
{
    var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(userIdStr, out var userId))
        return Results.BadRequest("Missing or invalid X-User-Id header.");

    VnpayOrderRequest? body;
    try
    {
        body = await JsonSerializer.DeserializeAsync<VnpayOrderRequest>(
            ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch { return Results.BadRequest("Invalid request body."); }
    if (body == null) return Results.BadRequest("Invalid request body.");

    var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    log.LogInformation("CreateOrder: userId={UserId} packageId={PackageId}", userId, body.PackageId);
    try
    {
        var ipAddr = ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
        var paymentUrl = await vnPay.CreateOrderUrlAsync(userId, body.PackageId, ipAddr);
        return Results.Ok(new { PaymentUrl = paymentUrl });
    }
    catch (InvalidOperationException ex)
    {
        log.LogWarning("CreateOrder 404: {Message}", ex.Message);
        return Results.NotFound(ex.Message);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "CreateOrder failed for user {UserId} package {PackageId}", userId, body.PackageId);
        return Results.Problem(ex.Message);
    }
});

// --- GET /vnpay/return (browser redirect after payment) ---
app.MapGet("/vnpay/return", async (HttpRequest req, IVnPayService vnPay) =>
{
    var query       = req.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
    var redirectUrl = await vnPay.ProcessReturnAsync(query);
    return Results.Redirect(redirectUrl);
});

// --- GET /vnpay/ipn (VNPay server-to-server callback) ---
app.MapGet("/vnpay/ipn", async (HttpRequest req, IVnPayService vnPay) =>
{
    var query  = req.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
    var result = await vnPay.ProcessIpnAsync(query);
    return Results.Json(new { RspCode = result.RspCode, Message = result.Message });
});

// --- GET /credits/balance ---
app.MapGet("/credits/balance", async (HttpContext ctx, IWalletRepository walletRepo, IOrderRepository orderRepo) =>
{
    var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(userIdStr, out var userId))
        return Results.BadRequest("Missing or invalid X-User-Id header.");

    var wallet         = await walletRepo.GetByUserIdAsync(userId);
    var totalPurchased = await orderRepo.GetTotalCreditsByUserAsync(userId);

    return Results.Ok(new
    {
        Balance        = wallet?.Balance ?? 0,
        TotalPurchased = totalPurchased,
        LastUpdatedAt  = wallet?.UpdatedAt
    });
});

// --- GET /credits/ledger ---
// Lịch sử biến động credit của user (bảng CreditLedger): Usage (trừ khi phỏng vấn, CreditChange âm),
// Purchase (mua gói, dương), Refund (hoàn, dương). Lọc ?type=Usage|Purchase|Refund, phân trang.
app.MapGet("/credits/ledger", async (
    HttpContext ctx, PaymentDbContext db,
    string? type = null, int page = 1, int pageSize = 10) =>
{
    var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(userIdStr, out var userId))
        return Results.BadRequest("Missing or invalid X-User-Id header.");

    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 50);

    var query = db.Set<CreditLedgerEntry>().AsNoTracking().Where(l => l.UserId == userId);
    if (!string.IsNullOrWhiteSpace(type))
        query = query.Where(l => l.EntryType == type);

    var totalCount = await query.CountAsync();

    // Tổng theo loại (trên TOÀN BỘ ledger của user, không theo filter) cho các stat card.
    var allOfUser = db.Set<CreditLedgerEntry>().AsNoTracking().Where(l => l.UserId == userId);
    var totalUsed = await allOfUser.Where(l => l.EntryType == "Usage").SumAsync(l => (int?)-l.CreditChange) ?? 0;
    var totalRefunded = await allOfUser.Where(l => l.EntryType == "Refund").SumAsync(l => (int?)l.CreditChange) ?? 0;

    var items = await query
        .OrderByDescending(l => l.CreatedAt)
        .Skip((page - 1) * pageSize).Take(pageSize)
        .Select(l => new
        {
            l.LedgerId,
            l.EntryType,
            l.CreditChange,
            l.BalanceAfter,
            l.ReferenceId,
            l.Description,
            l.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(new
    {
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        TotalUsed = totalUsed,
        TotalRefunded = totalRefunded,
        Items = items
    });
});

// --- Admin: GET /admin/credit-packages ---
app.MapGet("/admin/credit-packages", async (ICreditPackageRepository repo) =>
{
    var packages = await repo.GetAllAsync();
    return Results.Ok(packages.Cast<CreditPackage>().OrderBy(p => p.SortOrder));
});

// --- Admin: GET /admin/credit-packages/{id} ---
app.MapGet("/admin/credit-packages/{id:guid}", async (Guid id, ICreditPackageRepository repo) =>
{
    var pkg = await repo.GetByIdAsync(id);
    return pkg is null ? Results.NotFound() : Results.Ok(pkg);
});

// --- Admin: POST /admin/credit-packages ---
app.MapPost("/admin/credit-packages", async (HttpContext ctx, ICreditPackageRepository repo) =>
{
    AdminPackageRequest? body;
    try { body = await JsonSerializer.DeserializeAsync<AdminPackageRequest>(ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
    catch { return Results.BadRequest("Invalid request body."); }
    if (body is null) return Results.BadRequest();

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
});

// --- Admin: PUT /admin/credit-packages/{id} ---
app.MapPut("/admin/credit-packages/{id:guid}", async (Guid id, HttpContext ctx, ICreditPackageRepository repo) =>
{
    var pkg = await repo.GetByIdAsync(id);
    if (pkg is null) return Results.NotFound();

    AdminPackageRequest? body;
    try { body = await JsonSerializer.DeserializeAsync<AdminPackageRequest>(ctx.Request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
    catch { return Results.BadRequest("Invalid request body."); }
    if (body is null) return Results.BadRequest();

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
});

// --- Admin: DELETE /admin/credit-packages/{id} (soft delete) ---
app.MapDelete("/admin/credit-packages/{id:guid}", async (Guid id, ICreditPackageRepository repo) =>
{
    var pkg = await repo.GetByIdAsync(id);
    if (pkg is null) return Results.NotFound();

    pkg.IsActive  = false;
    pkg.UpdatedAt = DateTime.UtcNow.AddHours(7);
    await repo.SaveChangesAsync();
    return Results.NoContent();
});

// --- Admin: GET /admin/payments (filter + search + aggregates) ---
app.MapGet("/admin/payments", async (
    PaymentDbContext db,
    int page = 1, int pageSize = 20,
    string? keyword = null, string? status = null, string? paymentMethod = null,
    DateTime? dateFrom = null, DateTime? dateTo = null,
    decimal? minAmount = null, decimal? maxAmount = null) =>
{
    pageSize = Math.Clamp(pageSize, 1, 100);
    page     = Math.Max(1, page);

    var orders = db.Orders.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(keyword))
    {
        var kw = keyword.Trim();
        orders = orders.Where(o => o.OrderCode.Contains(kw));
    }
    if (!string.IsNullOrWhiteSpace(status))
        orders = orders.Where(o => o.Status == status);
    if (!string.IsNullOrWhiteSpace(paymentMethod))
        orders = orders.Where(o => o.PaymentMethod == paymentMethod);
    if (dateFrom.HasValue)
        orders = orders.Where(o => o.CreatedAt >= dateFrom.Value);
    if (dateTo.HasValue)
        orders = orders.Where(o => o.CreatedAt < dateTo.Value.AddDays(1));
    if (minAmount.HasValue)
        orders = orders.Where(o => o.Amount >= minAmount.Value);
    if (maxAmount.HasValue)
        orders = orders.Where(o => o.Amount <= maxAmount.Value);

    var totalCount  = await orders.CountAsync();
    var totalAmount = await orders.SumAsync(o => (decimal?)o.Amount) ?? 0m;
    var paidAmount  = await orders.Where(o => o.Status == "Paid").SumAsync(o => (decimal?)o.Amount) ?? 0m;
    var paidCount   = await orders.CountAsync(o => o.Status == "Paid");

    var items = await orders
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(o => new
        {
            o.OrderId, o.OrderCode, o.UserId, o.Amount, o.Credits,
            o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
        })
        .ToListAsync();

    var userNames = new Dictionary<Guid, string?>();
    if (items.Count > 0)
    {
        try
        {
            var ids = items.Select(i => i.UserId).Distinct().ToList();
            var inClause = string.Join(",", ids.Select((_, i) => $"@u{i}"));
            var sql = $@"
                SELECT a.AccountId, COALESCE(NULLIF(p.FullName, ''), a.Email) AS DisplayName
                FROM DevPrep_Auth.dbo.Accounts a
                LEFT JOIN DevPrep_User.dbo.UserProfiles p ON p.AccountId = a.AccountId
                WHERE a.AccountId IN ({inClause});";
            await using var conn = new SqlConnection(db.Database.GetConnectionString());
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            for (var i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@u{i}", ids[i]);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                userNames[reader.GetGuid(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        catch { }
    }

    return Results.Ok(new
    {
        TotalCount = totalCount, Page = page, PageSize = pageSize,
        TotalAmount = totalAmount, PaidAmount = paidAmount, PaidCount = paidCount,
        Items = items.Select(o => new
        {
            o.OrderId, o.OrderCode, o.UserId,
            UserName = userNames.GetValueOrDefault(o.UserId),
            o.Amount, o.Credits, o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
        })
    });
});

// --- Admin: GET /admin/payments/revenue-report ---
app.MapGet("/admin/payments/revenue-report", async (
    PaymentDbContext db,
    string period = "today", DateTime? from = null, DateTime? to = null) =>
{
    var now = DateTime.UtcNow.AddHours(7); // giờ Việt Nam, khớp quy ước chung của codebase
    DateTime rangeFrom, rangeTo;

    switch (period.ToLowerInvariant())
    {
        case "month":
            rangeFrom = new DateTime(now.Year, now.Month, 1);
            rangeTo   = rangeFrom.AddMonths(1);
            break;
        case "year":
            rangeFrom = new DateTime(now.Year, 1, 1);
            rangeTo   = rangeFrom.AddYears(1);
            break;
        case "custom":
            rangeFrom = (from ?? now.Date).Date;
            rangeTo   = (to ?? now.Date).Date.AddDays(1);
            break;
        default:
            period    = "today";
            rangeFrom = now.Date;
            rangeTo   = rangeFrom.AddDays(1);
            break;
    }

    var paidOrders = db.Orders.AsNoTracking()
        .Where(o => o.Status == "Paid" && o.PaidAt >= rangeFrom && o.PaidAt < rangeTo);

    var packagesSold = await paidOrders.CountAsync();
    var totalRevenue = await paidOrders.SumAsync(o => (decimal?)o.Amount) ?? 0m;
    var totalCredits = await paidOrders.SumAsync(o => (int?)o.Credits) ?? 0;

    return Results.Ok(new
    {
        Period = period,
        From = rangeFrom,
        To = rangeTo,
        PackagesSold = packagesSold,
        TotalRevenue = totalRevenue,
        TotalCredits = totalCredits
    });
});

// --- Admin: GET /admin/payments/revenue-daily ---
// Doanh thu theo TỪNG NGÀY trong khoảng [from..to] (yyyy-MM-dd, inclusive) — phục vụ chart dashboard.
// Cùng quy ước với revenue-report: chỉ tính Orders Status='Paid', mốc thời gian theo PaidAt (giờ VN).
app.MapGet("/admin/payments/revenue-daily", async (
    PaymentDbContext db,
    DateTime? from = null, DateTime? to = null) =>
{
    var now = DateTime.UtcNow.AddHours(7); // giờ Việt Nam, khớp quy ước chung của codebase
    var rangeFrom = (from ?? now.Date.AddDays(-29)).Date;   // mặc định: 30 ngày gần nhất
    var rangeToInclusive = (to ?? now.Date).Date;
    if (rangeToInclusive < rangeFrom)
        (rangeFrom, rangeToInclusive) = (rangeToInclusive, rangeFrom);
    var rangeTo = rangeToInclusive.AddDays(1);              // exclusive

    var days = await db.Orders.AsNoTracking()
        .Where(o => o.Status == "Paid" && o.PaidAt >= rangeFrom && o.PaidAt < rangeTo)
        .GroupBy(o => o.PaidAt!.Value.Date)
        .Select(g => new
        {
            Date = g.Key,
            Revenue = g.Sum(x => x.Amount),
            Orders = g.Count(),
            Credits = g.Sum(x => x.Credits)
        })
        .OrderBy(d => d.Date)
        .ToListAsync();

    return Results.Ok(new
    {
        From = rangeFrom,
        To = rangeToInclusive,
        TotalRevenue = days.Sum(d => d.Revenue),
        TotalOrders = days.Sum(d => d.Orders),
        TotalCredits = days.Sum(d => d.Credits),
        Days = days
    });
});

// --- Admin: GET /admin/payments/{id} ---
app.MapGet("/admin/payments/{id:guid}", async (Guid id, IOrderRepository orderRepo, ITransactionRepository txRepo) =>
{
    var order = await orderRepo.GetByIdAsync(id);
    if (order is null) return Results.NotFound();
    var tx = await txRepo.GetByOrderIdAsync(id);
    return Results.Ok(new { Order = order, Transaction = tx });
});

// --- Admin: POST /admin/credits/add ---
// Cộng credit cho account theo EMAIL (admin nhập tay). Ghi ledger EntryType='Refund'
// (yêu cầu nghiệp vụ: cộng tay hiển thị trong Credit History của user như một dòng Refund).
// Wallet chưa có thì tự tạo (giống luồng mua VNPay).
app.MapPost("/admin/credits/add", async (AdminCreditAdjustRequest req, IAuthAccountClient authClient, PaymentDbContext db, CancellationToken ct) =>
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

    using var transaction = await db.Database.BeginTransactionAsync(ct);
    try
    {
        var now = DateTime.UtcNow.AddHours(7);
        var wallet = await db.CreditWallets.FirstOrDefaultAsync(w => w.UserId == account.AccountId, ct);
        int newBalance;
        if (wallet is null)
        {
            newBalance = req.Amount;
            wallet = new CreditWallet { WalletId = Guid.NewGuid(), UserId = account.AccountId, Balance = newBalance, CreatedAt = now, UpdatedAt = now };
            db.CreditWallets.Add(wallet);
        }
        else
        {
            newBalance = wallet.Balance + req.Amount;
            wallet.Balance = newBalance;
            wallet.UpdatedAt = now;
        }

        db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            LedgerId = Guid.NewGuid(),
            UserId = account.AccountId,
            EntryType = "Refund",
            CreditChange = req.Amount,
            BalanceAfter = newBalance,
            ReferenceId = null,
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Admin credit adjustment" : req.Description.Trim(),
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { Success = true, account.Email, AccountId = account.AccountId, BalanceAfter = newBalance });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(ct);
        return Results.Json(new { Success = false, Message = $"Error: {ex.Message}" }, statusCode: 500);
    }
});

// --- Admin: POST /admin/credits/deduct ---
// Trừ credit theo EMAIL. Ghi ledger EntryType='Usage' (CreditChange âm). Chặn trừ quá số dư.
app.MapPost("/admin/credits/deduct", async (AdminCreditAdjustRequest req, IAuthAccountClient authClient, PaymentDbContext db, CancellationToken ct) =>
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

    using var transaction = await db.Database.BeginTransactionAsync(ct);
    try
    {
        var now = DateTime.UtcNow.AddHours(7);
        var wallet = await db.CreditWallets.FirstOrDefaultAsync(w => w.UserId == account.AccountId, ct);
        if (wallet is null)
            return Results.BadRequest(new { Success = false, Message = "This account has no credit wallet yet." });
        if (wallet.Balance < req.Amount)
            return Results.BadRequest(new { Success = false, Message = $"Insufficient credits. Available: {wallet.Balance}, requested: {req.Amount}." });

        wallet.Balance -= req.Amount;
        wallet.UpdatedAt = now;

        db.CreditLedgerEntries.Add(new CreditLedgerEntry
        {
            LedgerId = Guid.NewGuid(),
            UserId = account.AccountId,
            EntryType = "Usage",
            CreditChange = -req.Amount,
            BalanceAfter = wallet.Balance,
            ReferenceId = null,
            Description = string.IsNullOrWhiteSpace(req.Description) ? "Admin credit deduction" : req.Description.Trim(),
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new { Success = true, account.Email, AccountId = account.AccountId, BalanceAfter = wallet.Balance });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(ct);
        return Results.Json(new { Success = false, Message = $"Error: {ex.Message}" }, statusCode: 500);
    }
});

// --- GET /transactions ---
app.MapGet("/transactions", async (HttpContext ctx, IOrderRepository orderRepo, int page = 1, int pageSize = 10) =>
{
    var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(userIdStr, out var userId))
        return Results.BadRequest("Missing or invalid X-User-Id header.");

    pageSize = Math.Clamp(pageSize, 1, 50);
    page     = Math.Max(1, page);

    var result = await orderRepo.GetPagedByUserAsync(userId, page, pageSize);
    var items  = result.Items.Select(o => new
    {
        o.OrderId, o.OrderCode, o.Amount, o.Credits,
        o.Currency, o.PaymentMethod, o.Status, o.PaidAt, o.CreatedAt
    });
    return Results.Ok(new { TotalCount = result.TotalCount, Page = page, PageSize = pageSize, Items = items });
});

app.MapControllers();

app.Run();

record VnpayOrderRequest(Guid PackageId);

record AdminCreditAdjustRequest(string Email, int Amount, string? Description);

record AdminPackageRequest(
    string Name,
    string? Description,
    int Credits,
    int BonusCredits,
    decimal Price,
    string? Currency,
    int SortOrder,
    bool IsActive = true
);
