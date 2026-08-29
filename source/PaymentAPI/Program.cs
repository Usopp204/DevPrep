using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PaymentAPI.Data;
using PaymentAPI.Endpoints;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Only the real .env: loading .env.example too let a missing key resolve to a placeholder.
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

var servicePort = builder.Configuration.GetValue<int>("PAYMENT_API_PORT", 7008);
// Chỉ lắng nghe trên máy cục bộ. Service này nằm sau ApiGateway, không có lý do gì để lộ ra
// toàn mạng LAN — bind 0.0.0.0 nghĩa là bất kỳ máy nào cùng mạng đều gọi thẳng được, bỏ qua
// mọi kiểm soát ở gateway. Vẫn cho phép override qua SERVICE_BIND_HOST để chạy trong container
// (Docker bắt buộc bind 0.0.0.0 thì mới publish port ra ngoài được).
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

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

// Authentication alone never rejects anything — an endpoint has to ask for it. The /admin/*
// minimal-API routes below opt in through this policy so they stop relying on WebClient's
// cookie check, which anyone could bypass by calling the gateway directly.
// Manager is allowed as well as Admin on purpose: WebClient's PaymentAdminController.GuardAdmin
// accepts both, and the Manager dashboard reads /admin/payments/revenue-daily.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminArea", policy => policy.RequireRole("Admin", "Manager"));
});

// ── Repositories ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICreditPackageRepository, CreditPackageRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICreditPackageService, CreditPackageService>();
builder.Services.AddScoped<IVnPayService, VnPayService>();
builder.Services.AddScoped<InternalCreditService>();

// ── Khoá gọi nội bộ ───────────────────────────────────────────────────────────
// BẮT BUỘC phải cấu hình. Fail-closed: thiếu khoá thì service từ chối khởi động, thay vì
// âm thầm để /api/internal/* mở toang (trừ/hoàn/tặng credit) như hành vi cũ.
var internalApiKey = builder.Configuration.GetValue<string>("INTERNAL_API_KEY", string.Empty);
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException(
        "INTERNAL_API_KEY is missing. PaymentAPI refuses to start without it: " +
        "/api/internal/* (credit deduct/refund/award) would be reachable without authentication.");
}

// ── AuthAPI internal client (resolve email -> AccountId cho admin adjust credit) ──
var authBaseUrl = builder.Configuration.GetValue<string>("AUTH_API_BASEURL", "http://localhost:7001");
builder.Services.AddHttpClient<IAuthAccountClient, AuthAccountClient>(client =>
{
    client.BaseAddress = new Uri(authBaseUrl!);
    client.Timeout = TimeSpan.FromSeconds(15);
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

// ── Chặn /api/internal/*: chỉ service nội bộ cầm đúng khoá mới gọi được ───────────
// Vì sao cần: InternalCreditController (deduct/refund/award) và InternalPaymentActivityController
// không có [Authorize], AddAuthorization ở trên cũng không đặt FallbackPolicy, còn MapControllers()
// không kèm RequireAuthorization() -> hai controller đó vốn ẩn danh hoàn toàn. Trong khi gateway
// định tuyến "/api/payment/{**catch-all}" rồi cắt tiền tố, nên một request từ ngoài dạng
//     POST /api/payment/api/internal/credit/refund
// đi thẳng tới endpoint hoàn credit. Chặn ở tầng middleware (thay vì gắn attribute từng chỗ) để
// bao trọn cả controller lẫn minimal API, và để endpoint /api/internal/* thêm sau này tự động
// được bảo vệ mà không phải nhớ gắn gì.
var internalKeyBytes = Encoding.UTF8.GetBytes(internalApiKey);
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase))
    {
        var provided = context.Request.Headers["X-Internal-Api-Key"].ToString();
        // So sánh thời gian cố định: không rò rỉ độ dài khớp qua thời gian phản hồi.
        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), internalKeyBytes);

        if (!matches)
        {
            app.Logger.LogWarning(
                "Chặn lời gọi nội bộ thiếu/sai khoá: {Method} {Path} từ {RemoteIp}",
                context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// --- Health ---
app.MapGet("/health", () => Results.Ok(new { service = "Payment", status = "ok" }))
    .WithName("GetHealth")
    .WithOpenApi();

app.MapCreditPackageEndpoints();
app.MapVnPayEndpoints();
app.MapTransactionEndpoints();
app.MapCreditLedgerEndpoints();
app.MapAdminPaymentEndpoints();

// Package catalogue + VNPay routes: see Endpoints/CreditPackageEndpoints.cs
// and Endpoints/VnPayEndpoints.cs.

// --- GET /credits/balance ---
app.MapGet("/credits/balance", async (HttpContext ctx, ILedgerRepository ledgerRepo, IOrderRepository orderRepo) =>
{
    var userIdStr = ctx.Request.Headers["X-User-Id"].FirstOrDefault();
    if (!Guid.TryParse(userIdStr, out var userId))
        return Results.BadRequest("Missing or invalid X-User-Id header.");

    // Chỉ đọc nên không cần transaction: số dư = tổng biến động trên sổ cái.
    var balance        = await ledgerRepo.GetBalanceByUserAsync(userId);
    var lastChangedAt  = await ledgerRepo.GetLastChangedAtByUserAsync(userId);
    var totalPurchased = await orderRepo.GetTotalCreditsByUserAsync(userId);

    return Results.Ok(new
    {
        Balance        = balance,
        TotalPurchased = totalPurchased,
        LastUpdatedAt  = lastChangedAt
    });
});

// Admin credit-package maintenance: see Endpoints/CreditPackageEndpoints.cs.

// Admin payment monitoring: see Endpoints/AdminPaymentEndpoints.cs.

app.MapAdminCreditEndpoints();

// Buyer purchase history: see Endpoints/TransactionEndpoints.cs.

app.MapControllers();

app.Run();

