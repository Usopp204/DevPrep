using DevPrep.Contracts;
using MassTransit;
using UserAPI.Services.Interfaces;

namespace UserAPI.Consumers;

/// <summary>
/// Lắng nghe sự kiện AccountRegistered từ AuthAPI (qua RabbitMQ) và tạo hồ sơ
/// (UserProfiles) cho tài khoản mới. Uỷ thác logic cho IUserProfileService.
/// </summary>
public class AccountRegisteredConsumer : IConsumer<AccountRegistered>
{
    private readonly IUserProfileService _profileService;

    public AccountRegisteredConsumer(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    public Task Consume(ConsumeContext<AccountRegistered> context) =>
        _profileService.CreateProfileIfNotExistsAsync(context.Message.AccountId, context.Message.FullName);
}
