using System.Net.Http.Json;
using AuthAPI.Services.Interfaces;
using DevPrep.Contracts;

namespace AuthAPI.Services;

/// <summary>
/// Typed HttpClient gọi sang NotificationAPI. BaseAddress + header khoá nội bộ
/// (X-Internal-Api-Key) được cấu hình ở Program.cs khi AddHttpClient.
/// </summary>
public class NotificationApiClient : INotificationApiClient
{
    private readonly HttpClient _httpClient;

    public NotificationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendEmailVerificationAsync(EmailVerificationRequested message, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/internal/notifications/email-verification", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendPasswordResetAsync(PasswordResetRequested message, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/internal/notifications/password-reset", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
