using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WebClient.Services.Shared;

/// <summary>
/// Gắn Bearer token (cookie access_token) vào mọi request đi backend, và TỰ ĐỘNG REFRESH
/// khi JWT sắp/đã hết hạn (access token chỉ sống 30 phút — phiên phỏng vấn 30-60 phút sẽ
/// chết giữa chừng nếu không refresh). Cơ chế:
///  - Trước khi gửi: đọc exp trong JWT; còn dưới ExpiryMarginSeconds -> gọi api/auth/refresh
///    bằng cookie refresh_token (AuthAPI xoay vòng refresh token) rồi ghi cặp token mới vào
///    cookie + HttpContext.Items (các call sau trong cùng request dùng ngay token mới).
///  - Chống đua khi nhiều request song song cùng refresh 1 token (rotation làm kẻ thua 401):
///    khoá single-flight + cache kết quả theo refresh token cũ trong RefreshResultTtl.
///  - Refresh lỗi (refresh token hết hạn/bị thu hồi) -> gửi token cũ như trước, backend 401
///    như hành vi cũ; không phá luồng nào.
/// </summary>
public class JwtDelegatingHandler : DelegatingHandler
{
    private const int ExpiryMarginSeconds = 60;
    private const string AccessTokenItem = "jwt.access_token";
    private const string RefreshTokenItem = "jwt.refresh_token";
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private static readonly TimeSpan RefreshResultTtl = TimeSpan.FromSeconds(30);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;

    public JwtDelegatingHandler(IHttpContextAccessor httpContextAccessor, IMemoryCache cache)
    {
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            var token = context.Items[AccessTokenItem] as string ?? context.Request.Cookies["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                if (IsExpiringSoon(token))
                {
                    var refreshed = await TryRefreshAsync(context, request, cancellationToken);
                    if (!string.IsNullOrEmpty(refreshed))
                    {
                        token = refreshed;
                    }
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>JWT hết hạn hoặc còn dưới ExpiryMarginSeconds giây (parse claim exp, không verify chữ ký).</summary>
    private static bool IsExpiringSoon(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return false;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            if (doc.RootElement.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var exp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(exp) <= DateTimeOffset.UtcNow.AddSeconds(ExpiryMarginSeconds);
            }
        }
        catch
        {
            // Token không parse được -> cứ gửi nguyên trạng, backend tự trả 401.
        }

        return false;
    }

    private async Task<string?> TryRefreshAsync(HttpContext context, HttpRequestMessage originalRequest, CancellationToken cancellationToken)
    {
        var refreshToken = context.Items[RefreshTokenItem] as string ?? context.Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(refreshToken) || originalRequest.RequestUri is null)
        {
            return null;
        }

        var cacheKey = "jwt-refresh:" + refreshToken;
        if (_cache.TryGetValue(cacheKey, out (string Access, string Refresh) cached))
        {
            ApplyTokens(context, cached.Access, cached.Refresh);
            return cached.Access;
        }

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Request song song khác có thể vừa refresh xong trong lúc mình chờ khoá.
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                ApplyTokens(context, cached.Access, cached.Refresh);
                return cached.Access;
            }

            // Gọi thẳng xuống pipeline (base.SendAsync) tới gateway — không đệ quy qua handler này.
            var refreshUri = new Uri(originalRequest.RequestUri, "/api/auth/refresh");
            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUri)
            {
                Content = JsonContent.Create(new { refreshToken })
            };

            using var response = await base.SendAsync(refreshRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;
            if (!root.TryGetProperty("success", out var success) || !success.GetBoolean() ||
                !root.TryGetProperty("data", out var data))
            {
                return null;
            }

            var newAccess = data.TryGetProperty("accessToken", out var a) ? a.GetString() : null;
            var newRefresh = data.TryGetProperty("refreshToken", out var r) ? r.GetString() : null;
            if (string.IsNullOrEmpty(newAccess) || string.IsNullOrEmpty(newRefresh))
            {
                return null;
            }

            // Cache theo refresh token CŨ để các request song song còn cầm cookie cũ dùng lại kết quả.
            _cache.Set(cacheKey, (newAccess, newRefresh), RefreshResultTtl);
            ApplyTokens(context, newAccess, newRefresh);
            return newAccess;
        }
        catch
        {
            return null; // best-effort: lỗi mạng/parse -> giữ hành vi cũ (token cũ, backend 401)
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static void ApplyTokens(HttpContext context, string accessToken, string refreshToken)
    {
        // Các call tiếp theo trong CÙNG request đọc từ Items (cookie request không đổi được).
        context.Items[AccessTokenItem] = accessToken;
        context.Items[RefreshTokenItem] = refreshToken;

        // Ghi cookie mới cho các request sau (cùng options với StoreAuthCookies lúc login).
        if (!context.Response.HasStarted)
        {
            var opts = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            };
            context.Response.Cookies.Append("access_token", accessToken, opts);
            context.Response.Cookies.Append("refresh_token", refreshToken, opts);
        }
    }
}
