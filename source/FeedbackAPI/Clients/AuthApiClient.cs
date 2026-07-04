using System.Net.Http.Json;

namespace FeedbackAPI.Clients;

/// <summary>
/// Typed HttpClient gọi AuthAPI POST /internal/accounts/lookup để resolve Email.
/// BaseAddress + header X-Internal-Api-Key cấu hình ở Program.cs. Best-effort: lỗi -> trả rỗng.
/// </summary>
public class AuthApiClient : IAuthApiClient
{
    private static readonly IReadOnlyDictionary<Guid, string?> Empty = new Dictionary<Guid, string?>();

    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthApiClient> _logger;

    public AuthApiClient(HttpClient httpClient, ILogger<AuthApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetEmailsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default)
    {
        if (accountIds is null || accountIds.Count == 0) return Empty;

        try
        {
            var body = new LookupRequest(accountIds.Distinct().ToList());
            var response = await _httpClient.PostAsJsonAsync("/internal/accounts/lookup", body, cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<AccountLookupItem>>(cancellationToken)
                        ?? new List<AccountLookupItem>();

            return items
                .GroupBy(i => i.AccountId)
                .ToDictionary(g => g.Key, g => g.First().Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolve emails from AuthAPI failed; returning empty (best-effort).");
            return Empty;
        }
    }
}
