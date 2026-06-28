namespace InterviewAPI.Clients;

/// <summary>Gọi nội bộ AuthAPI để lấy Email theo AccountId. Best-effort (lỗi -> trả rỗng, không chặn luồng).</summary>
public interface IAuthApiClient
{
    Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);
}
