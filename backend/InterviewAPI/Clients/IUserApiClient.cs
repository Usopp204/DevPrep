namespace InterviewAPI.Clients;

/// <summary>Gọi nội bộ UserAPI để lấy FullName theo AccountId. Best-effort (lỗi -> trả rỗng, không chặn luồng).</summary>
public interface IUserApiClient
{
    Task<IReadOnlyDictionary<Guid, string?>> GetFullNamesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);
}
