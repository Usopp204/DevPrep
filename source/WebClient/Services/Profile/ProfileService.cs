using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WebClient.Models.Profile;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Profile;

public record ProfileResult(bool Success, bool Unauthorized, ProfileViewModel? Profile, string Message);

public record ProfileUpdateResult(bool Success, bool Unauthorized, string Message);

public record ChangePasswordResult(bool Success, bool Unauthorized, string Message);

public interface IProfileService
{
    Task<ProfileResult> GetMyProfileAsync();
    Task<ProfileUpdateResult> UpdateMyProfileAsync(EditProfileViewModel model);
    Task<ChangePasswordResult> ChangePasswordAsync(ChangePasswordViewModel model);
}

public sealed class ProfileService(IApiClient apiClient, IHttpContextAccessor httpContextAccessor) : IProfileService
{
    private const string CannotReach =
        "Cannot reach the server. Please make sure the API Gateway is running and try again.";

    public async Task<ProfileResult> GetMyProfileAsync()
    {
        var token = GetAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            return new ProfileResult(false, true, null, "Not authenticated.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/users/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await apiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ProfileResult(false, true, null, "Session expired. Please log in again.");
            }

            var body = await ReadBodyAsync<ProfileData>(response);
            if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
            {
                return new ProfileResult(true, false, new ProfileViewModel
                {
                    Email = body.Data.Email,
                    FullName = body.Data.FullName,
                    AvatarUrl = body.Data.AvatarUrl
                }, body.Message ?? "OK");
            }

            return new ProfileResult(false, false, null,
                body?.Message ?? $"Failed to load profile (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception)
        {
            return new ProfileResult(false, false, null, CannotReach);
        }
    }

    public async Task<ProfileUpdateResult> UpdateMyProfileAsync(EditProfileViewModel model)
    {
        var token = GetAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            return new ProfileUpdateResult(false, true, "Not authenticated.");
        }

        try
        {
            using var content = new MultipartFormDataContent
            {
                { new StringContent(model.FullName), "FullName" }
            };

            if (model.Avatar is { Length: > 0 })
            {
                var fileContent = new StreamContent(model.Avatar.OpenReadStream());
                if (!string.IsNullOrEmpty(model.Avatar.ContentType))
                {
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(model.Avatar.ContentType);
                }
                content.Add(fileContent, "Avatar", model.Avatar.FileName);
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, "api/users/me") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await apiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ProfileUpdateResult(false, true, "Session expired. Please log in again.");
            }

            var body = await ReadBodyAsync<ProfileData>(response);
            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return new ProfileUpdateResult(true, false, body.Message ?? "Profile updated successfully.");
            }

            return new ProfileUpdateResult(false, false,
                body?.Message ?? $"Update failed (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception)
        {
            return new ProfileUpdateResult(false, false, CannotReach);
        }
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(ChangePasswordViewModel model)
    {
        var token = GetAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            return new ChangePasswordResult(false, true, "Not authenticated.");
        }

        var payload = new
        {
            currentPassword = model.CurrentPassword,
            newPassword = model.NewPassword,
            confirmPassword = model.ConfirmPassword
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await apiClient.HttpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ChangePasswordResult(false, true, "Session expired. Please log in again.");
            }

            var body = await ReadBodyAsync<object>(response);
            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return new ChangePasswordResult(true, false, body.Message ?? "Password changed successfully.");
            }

            return new ChangePasswordResult(false, false,
                body?.Message ?? $"Change password failed (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception)
        {
            return new ChangePasswordResult(false, false, CannotReach);
        }
    }

    private string? GetAccessToken() =>
        httpContextAccessor.HttpContext?.Request.Cookies["access_token"];

    private static async Task<BackendResponse<T>?> ReadBodyAsync<T>(HttpResponseMessage response)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<BackendResponse<T>>();
        }
        catch
        {
            return null;
        }
    }
}
