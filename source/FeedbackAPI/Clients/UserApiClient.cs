using System.Net.Http.Json;

namespace FeedbackAPI.Clients;

/// <summary>
/// Typed HttpClient gọi UserAPI POST /internal/profiles/lookup để resolve FullName.
/// BaseAddress + header X-Internal-Api-Key cấu hình ở Program.cs. Best-effort: lỗi -> trả rỗng.
/// </summary>
public class UserApiClient : IUserApiClient
{
    private static readonly IReadOnlyDictionary<Guid, string?> Empty = new Dictionary<Guid, string?>();

    private readonly HttpClient _httpClient;
    private readonly ILogger<UserApiClient> _logger;

    public UserApiClient(HttpClient httpClient, ILogger<UserApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string?>> GetFullNamesAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default)
    {
        if (accountIds is null || accountIds.Count == 0) return Empty;

        try
        {
            var body = new LookupRequest(accountIds.Distinct().ToList());
            var response = await _httpClient.PostAsJsonAsync("/internal/profiles/lookup", body, cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<ProfileLookupItem>>(cancellationToken)
                        ?? new List<ProfileLookupItem>();

            return items
                .GroupBy(i => i.AccountId)
                .ToDictionary(g => g.Key, g => g.First().FullName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolve full names from UserAPI failed; returning empty (best-effort).");
            return Empty;
        }
    }
}
