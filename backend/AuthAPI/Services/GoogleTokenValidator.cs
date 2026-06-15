using AuthAPI.Exceptions;
using AuthAPI.Services.Interfaces;
using Google.Apis.Auth;

namespace AuthAPI.Services;

/// <summary>
/// Xác thực Google ID token (chữ ký, hạn, issuer, audience) bằng thư viện Google.Apis.Auth.
/// </summary>
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private const string GoogleProvider = "Google";
    private readonly string? _clientId;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IConfiguration configuration, ILogger<GoogleTokenValidator> logger)
    {
        _clientId = configuration["GOOGLE_CLIENT_ID"];
        _logger = logger;
    }

    public async Task<ExternalUserInfo> ValidateAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();

            // Nếu đã cấu hình Client ID thì kiểm tra audience để đảm bảo token phát cho app của ta.
            if (!string.IsNullOrWhiteSpace(_clientId))
            {
                settings.Audience = new[] { _clientId };
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new ExternalUserInfo(
                Provider: GoogleProvider,
                ProviderUserId: payload.Subject,
                Email: payload.Email,
                EmailVerified: payload.EmailVerified,
                Name: payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google ID token.");
            throw new UnauthorizedException("Invalid or expired Google token.");
        }
    }
}
