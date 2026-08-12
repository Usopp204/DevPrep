using DotNetEnv;
using Microsoft.AspNetCore.Http.Timeouts;

// Only the real .env: loading .env.example too let a missing key resolve to a placeholder.
try { Env.TraversePath().Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

// Gateway listen port (GLOBAL_)
var gatewayPort = builder.Configuration.GetValue<int>("GLOBAL_API_GATEWAY_PORT", 7000);
builder.WebHost.UseUrls($"http://0.0.0.0:{gatewayPort}");

// Body size limit (10MB)
var bodyLimitMb = builder.Configuration.GetValue<int>("Gateway:BodySizeLimitMb", 10);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = bodyLimitMb * 1024 * 1024;
});

// Global timeout (30s)
var timeoutSeconds = builder.Configuration.GetValue<int>("Gateway:RequestTimeoutSeconds", 30);
builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };
});

// CORS (Frontend -> Gateway)
// Trước đây là AllowAnyOrigin: bất kỳ trang web nào cũng gọi được API này bằng trình duyệt của
// người dùng đang đăng nhập. Nay chỉ mở cho đúng origin của WebClient. Danh sách đọc từ cấu hình
// (ngăn cách bằng dấu phẩy) để khi deploy chỉ cần đổi biến môi trường, không phải sửa mã.
// Lưu ý: WebClient gọi gateway ở phía server nên không đụng CORS; ràng buộc này áp cho các trang
// admin có JavaScript gọi thẳng gateway từ trình duyệt.
var corsOrigins = (builder.Configuration.GetValue<string>("CORS_ALLOWED_ORIGINS", "http://localhost:5130") ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseRequestTimeouts();
app.UseCors();

// ── Chặn mọi đường vòng tới API nội bộ ────────────────────────────────────────
// Nhiều route ở đây cắt tiền tố trước khi chuyển tiếp (PathRemovePrefix), nên phần đuôi do
// người gọi tự đặt sẽ trở thành đường dẫn thật ở service đích. Kẻ tấn công lợi dụng điều đó
// để với tới các endpoint nội bộ vốn không dành cho Internet, ví dụ:
//     POST /api/payment/api/internal/credit/refund   -> PaymentAPI  /api/internal/credit/refund
//     POST /api/admin/auth/internal/accounts/lookup  -> AuthAPI     /internal/accounts/lookup
//     POST /api/notifications/internal/notifications/email-verification -> NotificationAPI
// Không route công khai nào của hệ thống chứa đoạn "internal", và không trang nào ở WebClient
// gọi tới chúng (chỉ service gọi service, trực tiếp, không qua gateway) — nên chặn thẳng ở đây
// là an toàn và bao trọn cả những service chưa kịp tự bảo vệ mình.
// Đây là lớp phòng thủ thứ hai: PaymentAPI đã tự kiểm X-Internal-Api-Key, nhưng gateway không
// nên chuyển tiếp thứ mà nó vốn không có nhiệm vụ chuyển tiếp.
app.Use(async (context, next) =>
{
    foreach (var segment in context.Request.Path.Value?.Split('/') ?? Array.Empty<string>())
    {
        if (string.Equals(segment, "internal", StringComparison.OrdinalIgnoreCase))
        {
            app.Logger.LogWarning(
                "Chặn đường vòng tới API nội bộ qua gateway: {Method} {Path} từ {RemoteIp}",
                context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }

    await next();
});

// Liveness probe for the container HEALTHCHECK. Deliberately trivial — a literal 200 with no
// dependency checks: the gateway's job is to forward requests, and a probe that failed when a
// downstream service was down would take the whole stack's dependency graph with it.
// MUST be registered BEFORE MapReverseProxy() so the proxy's catch-all routes cannot swallow it.
app.MapGet("/health", () => Results.Ok(new { service = "ApiGateway", status = "ok" }));

app.MapReverseProxy();

app.Run();
