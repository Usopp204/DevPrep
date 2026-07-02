using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AuthAPI.Exceptions;
using AuthAPI.Services.Interfaces;

namespace AuthAPI.Services;

/// <summary>
/// Đổi authorization code của GitHub lấy access token và truy vấn thông tin người dùng.
/// GitHub không phát ID token (không OIDC) nên phải gọi REST API.
/// </summary>
public class GitHubOAuthClient : IGitHubOAuthClient
{
    private const string GitHubProvider = "GitHub";

    private readonly HttpClient _httpClient;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly ILogger<GitHubOAuthClient> _logger;

    public GitHubOAuthClient(HttpClient httpClient, IConfiguration configuration, ILogger<GitHubOAuthClient> logger)
    {
        _httpClient = httpClient;
        _clientId = configuration["GITHUB_CLIENT_ID"];
        _clientSecret = configuration["GITHUB_CLIENT_SECRET"];
        _logger = logger;

        // GitHub API bắt buộc có User-Agent.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DevPrep-AuthAPI");
    }

    public async Task<ExternalUserInfo> GetUserAsync(string code, string? redirectUri)
    {
        var accessToken = await ExchangeCodeAsync(code, redirectUri);
        var user = await GetAsync<GitHubUser>("https://api.github.com/user", accessToken)
            ?? throw new UnauthorizedException("Could not read GitHub user profile.");

        // Email ở /user có thể null (người dùng ẩn email) -> lấy email primary đã verify.
        var (email, verified) = await GetPrimaryVerifiedEmailAsync(accessToken);
        email ??= user.Email;

        return new ExternalUserInfo(
            Provider: GitHubProvider,
            ProviderUserId: user.Id.ToString(),
            Email: email,
            EmailVerified: verified,
            Name: string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name);
    }

    private async Task<string> ExchangeCodeAsync(string code, string? redirectUri)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _clientId ?? string.Empty,
            ["client_secret"] = _clientSecret ?? string.Empty,
            ["code"] = code
        };
        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            form["redirect_uri"] = redirectUri;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub token exchange failed with status {Status}.", response.StatusCode);
            throw new UnauthorizedException("Failed to exchange GitHub authorization code.");
        }

        var token = await response.Content.ReadFromJsonAsync<GitHubTokenResponse>();
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new UnauthorizedException("GitHub did not return an access token (code may be invalid or expired).");
        }

        return token.AccessToken;
    }

    private async Task<(string? email, bool verified)> GetPrimaryVerifiedEmailAsync(string accessToken)
    {
        var emails = await GetAsync<List<GitHubEmail>>("https://api.github.com/user/emails", accessToken);
        if (emails is null || emails.Count == 0)
        {
            return (null, false);
        }

        var primary = emails.FirstOrDefault(e => e.Primary && e.Verified)
            ?? emails.FirstOrDefault(e => e.Verified);

        return primary is null ? (null, false) : (primary.Email, primary.Verified);
    }

    private async Task<T?> GetAsync<T>(string url, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub GET {Url} failed with status {Status}.", url, response.StatusCode);
            throw new UnauthorizedException("Failed to fetch data from GitHub.");
        }

        return await response.Content.ReadFromJsonAsync<T>();
    }

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private sealed class GitHubUser
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("login")]
        public string? Login { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class GitHubEmail
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = null!;

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("verified")]
        public bool Verified { get; set; }
    }
}
