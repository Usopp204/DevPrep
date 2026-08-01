using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using WebClient.Services.Shared;

namespace WebClient.Middlewares;

/// <summary>
/// Chặn tài khoản bị block GIỮA PHIÊN: access token còn hạn (30-60') + cookie WebClient_Auth còn sống,
/// nên nếu chỉ dựa vào login thì user bị block vẫn thao tác tiếp được. Middleware này, trên mỗi
/// page-request của người đã đăng nhập, hỏi AuthAPI `GET api/auth/me/status` (cache 30s/account để
/// không dội gateway) — nếu Status=Blocked thì xoá toàn bộ cookie + sign-out + đá về trang Login.
/// Best-effort: AuthAPI/gateway chết hoặc trả lỗi -> cho đi tiếp (không khoá nhầm người dùng).
/// </summary>
public class BlockedAccountMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private static readonly TimeSpan StatusTtl = TimeSpan.FromSeconds(30);   // block có hiệu lực trong ≤30s
    private static readonly TimeSpan UnknownTtl = TimeSpan.FromSeconds(10);  // lỗi nguồn -> thử lại sau 10s

    public async Task InvokeAsync(HttpContext context, IApiClient apiClient)
    {
        var token = context.Request.Cookies["access_token"];

        // Chỉ check người đã đăng nhập, bỏ qua khu /Auth (login/logout/register... — tránh vòng lặp redirect).
        if (string.IsNullOrEmpty(token) ||
            context.Request.Path.StartsWithSegments("/Auth", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var accountId = JwtUserId.FromToken(token);
        if (accountId is null)
        {
            await next(context);
            return;
        }

        var cacheKey = $"acct-status:{accountId}";
        if (!cache.TryGetValue(cacheKey, out string? status))
        {
            status = await FetchStatusAsync(apiClient);
            cache.Set(cacheKey, status, status == "unknown" ? UnknownTtl : StatusTtl);
        }

        if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            // Đăng xuất cưỡng bức: xoá cookie token + cookie đọc được + phiên cookie-auth của MVC.
            foreach (var name in new[] { "access_token", "refresh_token", "user_email", "user_role" })
            {
                context.Response.Cookies.Delete(name);
            }
            context.Session.Clear();
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            cache.Remove(cacheKey);

            context.Response.Redirect("/Auth/Login?blocked=1");
            return;
        }

        await next(context);
    }

    /// <summary>Gọi AuthAPI qua gateway (Bearer do JwtDelegatingHandler tự gắn). Trả "unknown" khi không xác định được.</summary>
    private static async Task<string> FetchStatusAsync(IApiClient apiClient)
    {
        try
        {
            // Timeout riêng 5s — IApiClient dùng chung có timeout 6 phút (cho AI parse), không để status check treo page load.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var resp = await apiClient.HttpClient.GetAsync("api/auth/me/status", cts.Token);
            if (!resp.IsSuccessStatusCode) return "unknown";

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var st) &&
                st.ValueKind == JsonValueKind.String)
            {
                return st.GetString() ?? "unknown";
            }
            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
