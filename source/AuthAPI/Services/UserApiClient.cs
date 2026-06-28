using System.Net.Http.Json;
using AuthAPI.Services.Interfaces;
using DevPrep.Contracts;

namespace AuthAPI.Services;

/// <summary>
/// Typed HttpClient gọi sang UserAPI để tạo hồ sơ cho tài khoản mới. BaseAddress +
/// header khoá nội bộ (X-Internal-Api-Key) cấu hình ở Program.cs khi AddHttpClient.
/// </summary>
public class UserApiClient : IUserApiClient
{
    private readonly HttpClient _httpClient;

    public UserApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task CreateProfileAsync(AccountRegistered message, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/internal/profiles", message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
