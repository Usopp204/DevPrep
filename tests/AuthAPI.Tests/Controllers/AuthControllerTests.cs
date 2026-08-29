using System.Security.Claims;
using AuthAPI.Controllers;
using AuthAPI.DTOs;
using AuthAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AuthAPI.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_returns_created_response_and_forwards_request()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new RegisterRequest { Email = "user@example.com", Password = "Password1!", FullName = "Test User" };
        var expected = new RegisterResponse { AccountId = Guid.NewGuid(), Email = request.Email, Status = "Pending" };
        service.Setup(item => item.RegisterAsync(request)).ReturnsAsync(expected);

        var result = await controller.Register(request);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        var body = Assert.IsType<ApiResponse<RegisterResponse>>(response.Value);
        Assert.Equal(expected.AccountId, body.Data!.AccountId);
        service.Verify(item => item.RegisterAsync(request), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_returns_ok_with_active_verified_account()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new VerifyEmailOtpRequest { Email = "user@example.com", Otp = "123456" };
        var expected = new VerifyEmailOtpResponse { AccountId = Guid.NewGuid(), Email = request.Email, Status = "Active", IsEmailVerified = true };
        service.Setup(item => item.VerifyEmailOtpAsync(request)).ReturnsAsync(expected);

        var result = await controller.VerifyEmail(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<VerifyEmailOtpResponse>>(response.Value);
        Assert.True(body.Data!.IsEmailVerified);
        Assert.Equal("Active", body.Data.Status);
    }

    [Fact]
    public async Task Login_returns_tokens_from_service()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new LoginRequest { Email = "user@example.com", Password = "Password1!" };
        var expected = new LoginResponse { AccessToken = "access-token", RefreshToken = "refresh-token" };
        service.Setup(item => item.LoginAsync(request)).ReturnsAsync(expected);

        var result = await controller.Login(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponse>>(response.Value);
        Assert.Equal(expected.AccessToken, body.Data!.AccessToken);
        Assert.Equal(expected.RefreshToken, body.Data.RefreshToken);
    }

    [Fact]
    public async Task GoogleSso_returns_tokens_and_forwards_provider_token()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new GoogleLoginRequest { IdToken = "verified-google-id-token" };
        var expected = new LoginResponse { AccessToken = "access", RefreshToken = "refresh", Role = "Candidate" };
        service.Setup(item => item.GoogleLoginAsync(request)).ReturnsAsync(expected);

        var result = await controller.GoogleSso(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponse>>(response.Value);
        Assert.Same(expected, body.Data);
        service.Verify(item => item.GoogleLoginAsync(request), Times.Once);
    }

    [Fact]
    public async Task GitHubSso_propagates_provider_failure_without_returning_success()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new GitHubLoginRequest { Code = "expired-code" };
        service.Setup(item => item.GitHubLoginAsync(request)).ThrowsAsync(new InvalidOperationException("provider rejected code"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => controller.GitHubSso(request));

        Assert.Equal("provider rejected code", exception.Message);
        service.Verify(item => item.GitHubLoginAsync(request), Times.Once);
    }

    [Fact]
    public async Task ResendOtp_returns_expiration_from_service()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new ResendEmailOtpRequest { Email = "user@example.com" };
        var expected = new ResendEmailOtpResponse { Email = request.Email, ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        service.Setup(item => item.ResendEmailOtpAsync(request)).ReturnsAsync(expected);

        var result = await controller.ResendOtp(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<ResendEmailOtpResponse>>(response.Value);
        Assert.Equal(expected.ExpiresAt, body.Data!.ExpiresAt);
        service.Verify(item => item.ResendEmailOtpAsync(request), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_preserves_generic_non_enumerating_message()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new ForgotPasswordRequest { Email = "unknown@example.com" };
        var expected = new ForgotPasswordResponse { Message = "If the account exists, a reset link was sent." };
        service.Setup(item => item.ForgotPasswordAsync(request)).ReturnsAsync(expected);

        var result = await controller.ForgotPassword(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<ForgotPasswordResponse>>(response.Value);
        Assert.Equal(expected.Message, body.Message);
        Assert.Equal(expected.Message, body.Data!.Message);
    }

    [Fact]
    public async Task AdminLogin_uses_admin_service_path()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new LoginRequest { Email = "admin@example.com", Password = "Password1!" };
        var expected = new LoginResponse { Role = "Admin", AccessToken = "admin-access", RefreshToken = "admin-refresh" };
        service.Setup(item => item.AdminLoginAsync(request)).ReturnsAsync(expected);

        var result = await controller.AdminLogin(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LoginResponse>>(response.Value);
        Assert.Equal("Admin", body.Data!.Role);
        service.Verify(item => item.AdminLoginAsync(request), Times.Once);
        service.Verify(item => item.LoginAsync(It.IsAny<LoginRequest>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_returns_unauthorized_for_invalid_identity()
    {
        var service = new Mock<IAuthService>();
        var controller = CreateController(service, new Claim(ClaimTypes.NameIdentifier, "invalid"));

        var result = await controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "new" });

        Assert.IsType<UnauthorizedResult>(result);
        service.Verify(item => item.ChangePasswordAsync(It.IsAny<Guid>(), It.IsAny<ChangePasswordRequest>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_uses_authenticated_account_id()
    {
        var service = new Mock<IAuthService>();
        var accountId = Guid.NewGuid();
        var request = new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "new" };
        var expected = new ChangePasswordResponse { Email = "user@example.com", Message = "Password changed successfully." };
        var controller = CreateController(service, new Claim(ClaimTypes.NameIdentifier, accountId.ToString()));
        service.Setup(item => item.ChangePasswordAsync(accountId, request)).ReturnsAsync(expected);

        var result = await controller.ChangePassword(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<ChangePasswordResponse>>(response.Value);
        Assert.Equal(expected.Message, body.Data!.Message);
        service.Verify(item => item.ChangePasswordAsync(accountId, request), Times.Once);
    }

    [Fact]
    public async Task MyStatus_returns_blocked_fallback_when_account_is_missing()
    {
        var service = new Mock<IAuthService>();
        var accountId = Guid.NewGuid();
        var controller = CreateController(service, new Claim(ClaimTypes.NameIdentifier, accountId.ToString()));
        service.Setup(item => item.GetAccountStatusAsync(accountId)).ReturnsAsync((AccountStatusResponse?)null);

        var result = await controller.MyStatus();

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<AccountStatusResponse>>(response.Value);
        Assert.Equal("Blocked", body.Data!.Status);
        Assert.Empty(body.Data.Role);
    }

    [Fact]
    public async Task Logout_returns_success_response_and_forwards_request()
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var request = new LogoutRequest { RefreshToken = "refresh-token" };
        var expected = new LogoutResponse { Message = "Logged out successfully." };
        service.Setup(item => item.LogoutAsync(request)).ReturnsAsync(expected);

        var result = await controller.Logout(request);

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<LogoutResponse>>(response.Value);
        Assert.Equal(expected.Message, body.Data!.Message);
        service.Verify(item => item.LogoutAsync(request), Times.Once);
    }

    private static AuthController CreateController(Mock<IAuthService> service, Claim claim)
    {
        return new AuthController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { claim }))
                }
            }
        };
    }
}
