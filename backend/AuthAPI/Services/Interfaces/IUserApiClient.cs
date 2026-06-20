using DevPrep.Contracts;

namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Gọi UserAPI qua HTTP (thay cho message bus RabbitMQ) để tạo hồ sơ (UserProfiles)
/// khi có tài khoản mới (đăng ký thường hoặc SSO). Idempotent ở phía UserAPI.
/// </summary>
public interface IUserApiClient
{
    Task CreateProfileAsync(AccountRegistered message, CancellationToken cancellationToken = default);
}
