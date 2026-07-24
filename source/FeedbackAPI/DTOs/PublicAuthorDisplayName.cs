using FeedbackAPI.Clients;

namespace FeedbackAPI.DTOs;

public static class PublicAuthorDisplayName
{
    public const string Fallback = "DevPrep User";

    public static string FromProfile(string? fullName, bool isAnonymous = false)
    {
        if (isAnonymous)
        {
            return "Anonymous user";
        }

        return string.IsNullOrWhiteSpace(fullName) ? Fallback : fullName.Trim();
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveBatchAsync(
        IUserApiClient userApiClient,
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken = default)
    {
        var ids = accountIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string?> names;
        try
        {
            names = await userApiClient.GetFullNamesAsync(ids, cancellationToken);
        }
        catch
        {
            names = new Dictionary<Guid, string?>();
        }

        return ids.ToDictionary(
            id => id,
            id => FromProfile(names.TryGetValue(id, out var fullName) ? fullName : null));
    }
}
