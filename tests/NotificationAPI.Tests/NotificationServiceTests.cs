using DevPrep.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;
using NotificationAPI.Services;
using NotificationAPI.Services.Interfaces;
using Xunit;

namespace NotificationAPI.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task Verification_uses_active_template_and_replaces_every_placeholder()
    {
        var (sut, templates, logs, sender) = Create(); var account = Guid.NewGuid();
        templates.Setup(x => x.GetActiveByCodeAsync("VERIFY_EMAIL")).ReturnsAsync(new EmailTemplate { Subject = "Verify now", BodyHtml = "{{otp}}|{{verifyUrl}}|{{email}}|{{minutes}}" });
        var message = new EmailVerificationRequested { AccountId = account, Email = "a@example.com", Otp = "123456", VerifyUrl = "https://verify", ExpiresAt = DateTime.UtcNow.AddHours(8).AddMinutes(10) };
        await sut.SendEmailVerificationAsync(message);
        sender.Verify(x => x.SendAsync("a@example.com", "Verify now", It.Is<string>(b => b.Contains("123456|https://verify|a@example.com|") && !b.Contains("{{")), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(x => x.AddAsync(It.Is<EmailLog>(l => l.UserId == account && l.TemplateCode == "VERIFY_EMAIL" && l.Status == "Sent" && l.SentAt != null)), Times.Once);
        logs.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Verification_uses_default_template_when_repository_has_none()
    {
        var (sut, templates, logs, sender) = Create(); templates.Setup(x => x.GetActiveByCodeAsync("VERIFY_EMAIL")).ReturnsAsync((EmailTemplate?)null);
        await sut.SendEmailVerificationAsync(new EmailVerificationRequested { Email = "default@example.com", Otp = "654321", VerifyUrl = null, ExpiresAt = DateTime.MinValue });
        sender.Verify(x => x.SendAsync("default@example.com", It.Is<string>(s => !string.IsNullOrWhiteSpace(s)), It.Is<string>(b => b.Contains("654321") && !b.Contains("{{otp}}") && !b.Contains("{{verifyUrl}}")), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(x => x.AddAsync(It.Is<EmailLog>(l => l.Body!.Contains("1") && l.Status == "Sent")), Times.Once);
    }

    [Fact]
    public async Task Verification_records_failed_log_when_sender_throws()
    {
        var (sut, templates, logs, sender) = Create(); templates.Setup(x => x.GetActiveByCodeAsync(It.IsAny<string>())).ReturnsAsync((EmailTemplate?)null); sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("smtp down"));
        await sut.SendEmailVerificationAsync(new EmailVerificationRequested { Email = "fail@example.com", Otp = "1", ExpiresAt = DateTime.UtcNow });
        logs.Verify(x => x.AddAsync(It.Is<EmailLog>(l => l.Status == "Failed" && l.ErrorMessage == "smtp down" && l.SentAt == null)), Times.Once); logs.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Password_reset_uses_active_template_and_replaces_every_placeholder()
    {
        var (sut, templates, logs, sender) = Create(); var account = Guid.NewGuid(); templates.Setup(x => x.GetActiveByCodeAsync("PASSWORD_RESET")).ReturnsAsync(new EmailTemplate { Subject = "Reset now", BodyHtml = "{{resetUrl}}|{{email}}|{{minutes}}" });
        await sut.SendPasswordResetAsync(new PasswordResetRequested { AccountId = account, Email = "reset@example.com", ResetUrl = "https://reset", ExpiresAt = DateTime.UtcNow.AddHours(8).AddMinutes(15) });
        sender.Verify(x => x.SendAsync("reset@example.com", "Reset now", It.Is<string>(b => b.Contains("https://reset|reset@example.com|") && !b.Contains("{{")), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(x => x.AddAsync(It.Is<EmailLog>(l => l.UserId == account && l.TemplateCode == "PASSWORD_RESET" && l.Status == "Sent")), Times.Once);
    }

    [Fact]
    public async Task Password_reset_uses_default_template_when_repository_has_none()
    {
        var (sut, templates, logs, sender) = Create(); templates.Setup(x => x.GetActiveByCodeAsync("PASSWORD_RESET")).ReturnsAsync((EmailTemplate?)null);
        await sut.SendPasswordResetAsync(new PasswordResetRequested { Email = "default@example.com", ResetUrl = "https://reset/default", ExpiresAt = DateTime.MinValue });
        sender.Verify(x => x.SendAsync("default@example.com", It.Is<string>(s => !string.IsNullOrWhiteSpace(s)), It.Is<string>(b => b.Contains("https://reset/default") && b.Contains("default@example.com") && !b.Contains("{{")), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Password_reset_records_failed_log_when_sender_throws()
    {
        var (sut, templates, logs, sender) = Create(); templates.Setup(x => x.GetActiveByCodeAsync(It.IsAny<string>())).ReturnsAsync((EmailTemplate?)null); sender.Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("network"));
        await sut.SendPasswordResetAsync(new PasswordResetRequested { Email = "fail@example.com", ResetUrl = "https://reset", ExpiresAt = DateTime.UtcNow });
        logs.Verify(x => x.AddAsync(It.Is<EmailLog>(l => l.Status == "Failed" && l.ErrorMessage == "network")), Times.Once); logs.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_token_is_forwarded_to_sender(bool verification)
    {
        var (sut, templates, _, sender) = Create(); templates.Setup(x => x.GetActiveByCodeAsync(It.IsAny<string>())).ReturnsAsync((EmailTemplate?)null); using var source = new CancellationTokenSource();
        if (verification) await sut.SendEmailVerificationAsync(new EmailVerificationRequested { Email = "a@b.com", Otp = "1", ExpiresAt = DateTime.UtcNow.AddHours(8) }, source.Token);
        else await sut.SendPasswordResetAsync(new PasswordResetRequested { Email = "a@b.com", ResetUrl = "url", ExpiresAt = DateTime.UtcNow.AddHours(8) }, source.Token);
        sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), source.Token), Times.Once);
    }

    private static (NotificationService Sut, Mock<IEmailTemplateRepository> Templates, Mock<IEmailLogRepository> Logs, Mock<IEmailSender> Sender) Create()
    {
        var templates = new Mock<IEmailTemplateRepository>(); var logs = new Mock<IEmailLogRepository>(); var sender = new Mock<IEmailSender>();
        return (new NotificationService(templates.Object, logs.Object, sender.Object, NullLogger<NotificationService>.Instance), templates, logs, sender);
    }
}
