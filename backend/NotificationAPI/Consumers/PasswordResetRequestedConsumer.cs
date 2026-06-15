using DevPrep.Contracts;
using MassTransit;
using NotificationAPI.Services.Interfaces;

namespace NotificationAPI.Consumers;

/// <summary>
/// Lắng nghe sự kiện PasswordResetRequested từ AuthAPI (qua RabbitMQ) và uỷ thác
/// việc gửi email đặt lại mật khẩu cho INotificationService.
/// </summary>
public class PasswordResetRequestedConsumer : IConsumer<PasswordResetRequested>
{
    private readonly INotificationService _notificationService;

    public PasswordResetRequestedConsumer(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Consume(ConsumeContext<PasswordResetRequested> context) =>
        _notificationService.SendPasswordResetAsync(context.Message, context.CancellationToken);
}
