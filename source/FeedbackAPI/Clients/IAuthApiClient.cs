namespace FeedbackAPI.Clients;

/// <summary>Gọi nội bộ AuthAPI để lấy Email theo AccountId. Best-effort (lỗi -> trả rỗng, không chặn luồng).</summary>
public interface IAuthApiClient
{
    Task<IReadOnlyDictionary<Guid, string?>> GetEmailsAsync(
        IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);

    /// <summary>Tìm AccountId có Email chứa keyword (search feedback theo email). Lỗi -> rỗng.</summary>
    Task<IReadOnlyList<Guid>> SearchIdsByEmailAsync(string keyword, CancellationToken cancellationToken = default);
}
