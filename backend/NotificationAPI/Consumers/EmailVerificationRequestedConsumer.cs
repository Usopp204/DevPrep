using DevPrep.Contracts;
using MassTransit;
using NotificationAPI.Services.Interfaces;

namespace NotificationAPI.Consumers;

/// <summary>
/// Lắng nghe sự kiện EmailVerificationRequested từ AuthAPI (qua RabbitMQ) và uỷ thác
/// việc gửi email cho INotificationService.
/// </summary>
public class EmailVerificationRequestedConsumer : IConsumer<EmailVerificationRequested>
{
    private readonly INotificationService _notificationService;

    public EmailVerificationRequestedConsumer(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public Task Consume(ConsumeContext<EmailVerificationRequested> context) =>
        _notificationService.SendEmailVerificationAsync(context.Message, context.CancellationToken);
}
