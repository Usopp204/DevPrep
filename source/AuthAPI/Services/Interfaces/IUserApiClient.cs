using DevPrep.Contracts;

namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Gọi UserAPI qua HTTP (thay cho message bus RabbitMQ) để tạo hồ sơ (UserProfiles)
/// khi có tài khoản mới (đăng ký thường hoặc SSO). Idempotent ở phía UserAPI.
/// </summary>
public interface IUserApiClient
{
    Task CreateProfileAsync(AccountRegistered message, CancellationToken cancellationToken = default);

    /// <summary>Xoá hồ sơ của các account đã bị dọn (account Pending hết hạn xác thực).</summary>
    Task DeleteProfilesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken = default);
}
