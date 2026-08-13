using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using AuthAPI.Exceptions;
using AuthAPI.Services.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace AuthAPI.Services;

/// <summary>
/// Xác thực Firebase ID token mà KHÔNG cần service account:
///   - tải khóa công khai (JWKS) của Google securetoken (có cache),
///   - kiểm chữ ký RS256 + issuer (securetoken.google.com/&lt;projectId&gt;) + audience (&lt;projectId&gt;) + hết hạn,
///   - rút uid/email/name + map sign_in_provider (google.com/github.com…) sang Provider chuẩn.
/// </summary>
public class FirebaseTokenValidator : IFirebaseTokenValidator
{
    // Khóa công khai cho Firebase ID token (securetoken). Trả JWKS chuẩn, Google xoay khóa định kỳ.
    private const string JwksUrl =
        "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";

    // Cache khóa dùng chung giữa các request (typed client là transient nên để static).
    private static IReadOnlyCollection<SecurityKey>? _cachedKeys;
    private static DateTime _cacheExpiryUtc;
    private static readonly SemaphoreSlim _gate = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FirebaseTokenValidator> _logger;

    public FirebaseTokenValidator(HttpClient httpClient, IConfiguration configuration, ILogger<FirebaseTokenValidator> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ExternalUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var projectId = _configuration["Firebase:ProjectId"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Firebase:ProjectId is not configured (appsettings or environment variable).");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new BadRequestException("idToken is required.");
        }

        try
        {
            RunValidation(idToken, projectId, await GetSigningKeysAsync(forceRefresh: false, cancellationToken));
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // Khóa ký có thể vừa được Google xoay vòng (kid mới chưa có trong cache)
            // -> ép tải lại JWKS rồi thử verify lại đúng 1 lần.
            try
            {
                RunValidation(idToken, projectId, await GetSigningKeysAsync(forceRefresh: true, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Firebase ID token verification failed after refreshing the public keys.");
                throw new BadRequestException("The sign-in token is invalid or has expired.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase ID token verification failed.");
            throw new BadRequestException("The sign-in token is invalid or has expired.");
        }

        // Token đã được verify -> đọc lại payload (an toàn) để lấy các claim, kể cả claim lồng "firebase".
        var segments = idToken.Split('.');
        if (segments.Length < 2)
        {
            throw new BadRequestException("The sign-in token is malformed.");
        }

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Base64UrlDecode(segments[1])));
        var root = doc.RootElement;

        var uid = GetString(root, "user_id") ?? GetString(root, "sub");
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new BadRequestException("The sign-in token carries no user identifier.");
        }

        var email = GetString(root, "email");
        var emailVerified = root.TryGetProperty("email_verified", out var ev) && ev.ValueKind == JsonValueKind.True;
        var name = GetString(root, "name");

        // Provider lấy từ claim firebase.sign_in_provider (fail-closed: thiếu/không hỗ trợ -> MapProvider ném lỗi).
        var hasFirebase = root.TryGetProperty("firebase", out var firebase);
        var signInProvider = hasFirebase && firebase.TryGetProperty("sign_in_provider", out var sp)
            ? sp.GetString()
            : null;
        var provider = MapProvider(signInProvider);

        // ProviderUserId: ưu tiên id của nhà cung cấp gốc (firebase.identities[provider][0]) để KHỚP
        // luồng /sso/google, /sso/github cũ (tránh tạo SsoIdentity trùng cho cùng 1 người); fallback Firebase uid.
        var providerUserId = uid!;
        if (hasFirebase
            && signInProvider is not null
            && firebase.TryGetProperty("identities", out var identities)
            && identities.TryGetProperty(signInProvider, out var providerIds)
            && providerIds.ValueKind == JsonValueKind.Array
            && providerIds.GetArrayLength() > 0
            && providerIds[0].ValueKind == JsonValueKind.String)
        {
            providerUserId = providerIds[0].GetString()!;
        }

        return new ExternalUserInfo(provider, providerUserId, email, emailVerified, name);
    }

    // Verify chữ ký RS256 + issuer/audience/lifetime với bộ khóa cho trước.
    // Ném SecurityTokenSignatureKeyNotFoundException nếu kid không khớp khóa nào (để gọi retry refresh).
    private static void RunValidation(string idToken, string projectId, IReadOnlyCollection<SecurityKey> keys)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(60),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys,
            ValidAlgorithms = new[] { "RS256" }
        };
        new JwtSecurityTokenHandler().ValidateToken(idToken, parameters, out _);
    }

    private async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cachedKeys is not null && DateTime.UtcNow < _cacheExpiryUtc)
        {
            return _cachedKeys;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _cachedKeys is not null && DateTime.UtcNow < _cacheExpiryUtc)
            {
                return _cachedKeys;
            }

            var json = await _httpClient.GetStringAsync(JwksUrl, cancellationToken);
            var keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
            if (keys.Count == 0)
            {
                throw new InvalidOperationException("Could not fetch the Google securetoken public keys.");
            }

            _cachedKeys = keys;
            _cacheExpiryUtc = DateTime.UtcNow.AddHours(1);
            return keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Firebase sign_in_provider -> giá trị Provider hợp lệ trong CHECK constraint (Google/GitHub/Facebook/Microsoft).
    private static string MapProvider(string? signInProvider) => signInProvider switch
    {
        "google.com" => "Google",
        "github.com" => "GitHub",
        "facebook.com" => "Facebook",
        "microsoft.com" => "Microsoft",
        _ => throw new BadRequestException($"Sign-in provider is not supported: {signInProvider}.")
    };

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
