using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using WebClient.Services.Shared;

namespace WebClient.Middlewares;

public sealed class AccountValidationMiddleware
{
    private static readonly TimeSpan StatusTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UnknownTtl = TimeSpan.FromSeconds(10);
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public AccountValidationMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IApiClient apiClient)
    {
        var token = context.Request.Cookies["access_token"];

        if (string.IsNullOrEmpty(token) ||
            context.Request.Path.StartsWithSegments("/Auth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var accountId = JwtUserId.FromToken(token);
        if (accountId is null)
        {
            await _next(context);
            return;
        }

        var cacheKey = $"acct-status:{accountId}";
        if (!_cache.TryGetValue(cacheKey, out AccountValidationResult validation))
        {
            validation = await FetchStatusAsync(apiClient);
            var ttl = validation.Status.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                ? UnknownTtl
                : StatusTtl;

            _cache.Set(cacheKey, validation, ttl);
        }

        if (string.Equals(validation.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            await SignOutAndRedirectAsync(context, "blocked");
            return;
        }

        var currentRole = context.Request.Cookies["user_role"]?.Trim() ?? string.Empty;
        var backendRole = validation.Role?.Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(currentRole) &&
            !string.IsNullOrEmpty(backendRole) &&
            !string.Equals(currentRole, backendRole, StringComparison.OrdinalIgnoreCase))
        {
            await SignOutAndRedirectAsync(context, "roleChanged");
            return;
        }

        await _next(context);
    }

    private static async Task SignOutAndRedirectAsync(HttpContext context, string reason)
    {
        foreach (var name in new[] { "access_token", "refresh_token", "user_email", "user_role" })
        {
            context.Response.Cookies.Delete(name);
        }

        context.Session.Clear();
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Redirect($"/Auth/Login?{reason}=1");
    }

    private static async Task<AccountValidationResult> FetchStatusAsync(IApiClient apiClient)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var resp = await apiClient.HttpClient.GetAsync("api/auth/me/status", cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                return AccountValidationResult.Unknown;
            }

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                return AccountValidationResult.Unknown;
            }

            var status = data.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;
            var role = data.TryGetProperty("role", out var roleProp) && roleProp.ValueKind == JsonValueKind.String
                ? roleProp.GetString() ?? string.Empty
                : string.Empty;

            return new AccountValidationResult(status, role);
        }
        catch
        {
            return AccountValidationResult.Unknown;
        }
    }

    private readonly record struct AccountValidationResult(string Status, string Role)
    {
        public static readonly AccountValidationResult Unknown = new("unknown", string.Empty);
    }
}
