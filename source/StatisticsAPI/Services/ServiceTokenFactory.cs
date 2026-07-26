using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace StatisticsAPI.Services;

public interface IServiceTokenFactory
{
    /// <summary>Mint 1 JWT "service account" role=Admin (ký cùng key AuthAPI) để gọi các admin endpoint của service khác.</summary>
    string CreateAdminToken();
}

/// <summary>
/// Sinh JWT nội bộ role=Admin — CÙNG Issuer/Audience/Key với AuthAPI nên mọi service (User/Interview/Feedback...)
/// đều verify được. Cấu trúc claim sao chép y hệt AuthAPI.TokenService để [Authorize(Roles="Admin")] chấp nhận.
/// </summary>
public sealed class ServiceTokenFactory : IServiceTokenFactory
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly string _key;

    // Account id "ảo" cho service — chỉ để điền claim sub/nameidentifier (không có account thật tương ứng).
    private static readonly string SystemAccountId = "00000000-0000-0000-0000-0000000057A7"; // "STAT"

    public ServiceTokenFactory(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? "DevPrep.AuthAPI";
        _audience = configuration["Jwt:Audience"] ?? "DevPrep.Client";
        _key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing in configuration.");
    }

    public string CreateAdminToken()
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, SystemAccountId),
            new(JwtRegisteredClaimNames.Email, "statistics-service@devprep.local"),
            new(ClaimTypes.NameIdentifier, SystemAccountId),
            new(ClaimTypes.Role, "Admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),   // token ngắn hạn, chỉ dùng trong 1 lần rollup
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
