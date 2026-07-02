using System.Net.Http.Json;
using WebClient.Models.Auth;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.Auth;

public interface IAuthService
{
    AuthPageViewModel GetPage(string pageName);
    Task<RegisterResult> RegisterAsync(RegisterViewModel model);
    Task<LoginResult> LoginAsync(LoginViewModel model);
    Task<AuthActionResult> VerifyEmailAsync(string email, string otp);
    Task<AuthActionResult> ResendOtpAsync(string email);
    Task<AuthActionResult> ForgotPasswordAsync(string email);
    Task<AuthActionResult> ResetPasswordAsync(ResetPasswordViewModel model);
    Task<LoginResult> SsoFirebaseAsync(string idToken);
    Task<AuthActionResult> LogoutAsync(string refreshToken);
}

public sealed class AuthService(IApiClient apiClient) : IAuthService
{
    private const string CannotReachMessage =
        "Cannot reach the server. Please make sure the API Gateway is running and try again.";

    public AuthPageViewModel GetPage(string pageName)
    {
        return new AuthPageViewModel
        {
            ModuleName = "AUTH + PROFILE",
            PageName = pageName,
            Epic = "AUTH + PROFILE",
            Summary = $"{pageName} page."
        };
    }

    public async Task<RegisterResult> RegisterAsync(RegisterViewModel model)
    {
        var payload = new
        {
            fullName = model.FullName,
            email = model.Email,
            password = model.Password,
            confirmPassword = model.ConfirmPassword
        };

        try
        {
            using var response = await apiClient.HttpClient.PostAsJsonAsync("api/auth/register", payload);
            var body = await ReadBodyAsync<RegisterResponseData>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return new RegisterResult(true, body.Message ?? "Registration successful.", body.Data?.Email ?? model.Email);
            }

            return new RegisterResult(false, body?.Message ?? $"Registration failed (HTTP {(int)response.StatusCode}).", null);
        }
        catch (Exception)
        {
            return new RegisterResult(false, CannotReachMessage, null);
        }
    }

    public async Task<LoginResult> LoginAsync(LoginViewModel model)
    {
        var payload = new { email = model.Email, password = model.Password };

        try
        {
            using var response = await apiClient.HttpClient.PostAsJsonAsync("api/auth/login", payload);
            var body = await ReadBodyAsync<LoginResponseData>(response);

            if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
            {
                return new LoginResult(true, body.Message ?? "Login successful.", body.Data);
            }

            return new LoginResult(false, body?.Message ?? $"Login failed (HTTP {(int)response.StatusCode}).", null);
        }
        catch (Exception)
        {
            return new LoginResult(false, CannotReachMessage, null);
        }
    }

    public async Task<LoginResult> SsoFirebaseAsync(string idToken)
    {
        var payload = new { idToken };

        try
        {
            using var response = await apiClient.HttpClient.PostAsJsonAsync("api/auth/sso/firebase", payload);
            var body = await ReadBodyAsync<LoginResponseData>(response);

            if (response.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
            {
                return new LoginResult(true, body.Message ?? "Login successful.", body.Data);
            }

            return new LoginResult(false, body?.Message ?? $"SSO login failed (HTTP {(int)response.StatusCode}).", null);
        }
        catch (Exception)
        {
            return new LoginResult(false, CannotReachMessage, null);
        }
    }

    public Task<AuthActionResult> VerifyEmailAsync(string email, string otp) =>
        PostSimpleAsync("api/auth/verify-email", new { email, otp }, "Email verified successfully.");

    public Task<AuthActionResult> ResendOtpAsync(string email) =>
        PostSimpleAsync("api/auth/resend-otp", new { email }, "A new verification code has been sent.");

    public Task<AuthActionResult> ForgotPasswordAsync(string email) =>
        PostSimpleAsync("api/auth/forgot-password", new { email },
            "If an account with that email exists, a reset link has been sent.");

    public Task<AuthActionResult> LogoutAsync(string refreshToken) =>
        PostSimpleAsync("api/auth/logout", new { refreshToken }, "Logged out.");

    public Task<AuthActionResult> ResetPasswordAsync(ResetPasswordViewModel model) =>
        PostSimpleAsync("api/auth/reset-password",
            new { token = model.Token, newPassword = model.NewPassword, confirmPassword = model.ConfirmPassword },
            "Your password has been reset. Please log in.");

    // ----- helpers -----

    private async Task<AuthActionResult> PostSimpleAsync(string path, object payload, string defaultSuccess)
    {
        try
        {
            using var response = await apiClient.HttpClient.PostAsJsonAsync(path, payload);
            var body = await ReadBodyAsync<object>(response);

            if (response.IsSuccessStatusCode && body?.Success == true)
            {
                return new AuthActionResult(true, body.Message ?? defaultSuccess);
            }

            return new AuthActionResult(false, body?.Message ?? $"Request failed (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception)
        {
            return new AuthActionResult(false, CannotReachMessage);
        }
    }

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
