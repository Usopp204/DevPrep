using System.Net.Http.Json;

namespace PaymentAPI.Services;

/// <summary>Kết quả resolve email -> account (email trả về là email CHÍNH XÁC trong DB).</summary>
public sealed record ResolvedAccount(Guid AccountId, string Email);

public interface IAuthAccountClient
{
    /// <summary>
    /// Tìm account theo email CHÍNH XÁC (không phân biệt hoa thường). Null nếu không có.
    /// Lưu ý PaymentAPI "UserId" (wallet/ledger) chính là AccountId (JWT sub).
    /// </summary>
    Task<ResolvedAccount?> FindByExactEmailAsync(string email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Typed HttpClient gọi endpoint nội bộ của AuthAPI (search-by-email là Contains nên phải
/// lookup lại để so khớp CHÍNH XÁC email). BaseAddress + X-Internal-Api-Key set ở Program.cs.
/// </summary>
public class AuthAccountClient : IAuthAccountClient
{
    private sealed record SearchByKeywordRequest(string Keyword);
    private sealed record AccountLookupRequest(List<Guid> AccountIds);
    private sealed record AccountLookupItem(Guid AccountId, string Email);

    private readonly HttpClient _httpClient;

    public AuthAccountClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResolvedAccount?> FindByExactEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim();

        var searchResponse = await _httpClient.PostAsJsonAsync(
            "/internal/accounts/search-by-email", new SearchByKeywordRequest(normalized), cancellationToken);
        searchResponse.EnsureSuccessStatusCode();
        var ids = await searchResponse.Content.ReadFromJsonAsync<List<Guid>>(cancellationToken: cancellationToken) ?? new();
        if (ids.Count == 0)
        {
            return null;
        }

        var lookupResponse = await _httpClient.PostAsJsonAsync(
            "/internal/accounts/lookup", new AccountLookupRequest(ids), cancellationToken);
        lookupResponse.EnsureSuccessStatusCode();
        var items = await lookupResponse.Content.ReadFromJsonAsync<List<AccountLookupItem>>(cancellationToken: cancellationToken) ?? new();

        var match = items.FirstOrDefault(i => string.Equals(i.Email, normalized, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : new ResolvedAccount(match.AccountId, match.Email);
    }
}
