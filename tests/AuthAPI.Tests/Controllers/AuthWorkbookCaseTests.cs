using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AuthAPI.Controllers;
using AuthAPI.DTOs;
using AuthAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AuthAPI.Tests.Controllers;

public class AuthWorkbookCaseTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return Case("FUNC001-USR", 1, "success");
        yield return Case("FUNC001-USR", 2, "duplicate");
        yield return Case("FUNC001-USR", 3, "empty-email");
        yield return Case("FUNC001-USR", 4, "empty-password");
        yield return Case("FUNC001-USR", 5, "empty-name");
        yield return Case("FUNC001-USR", 6, "dependency-error");

        yield return Case("FUNC002-USR", 1, "success");
        yield return Case("FUNC002-USR", 2, "rejected");
        yield return Case("FUNC002-USR", 3, "empty-email");
        yield return Case("FUNC002-USR", 4, "empty-password");
        yield return Case("FUNC002-USR", 5, "all-empty");

        foreach (var code in new[] { "FUNC003-USR", "FUNC004-USR", "FUNC005-USR", "FUNC006-USR", "FUNC007-USR", "FUNC016-USR", "FUNC083-ADM" })
        {
            yield return Case(code, 1, "success");
            yield return Case(code, 2, "invalid-format");
            yield return Case(code, 3, "empty");
            yield return Case(code, 4, "not-found");
            yield return Case(code, 5, "boundary");
            yield return Case(code, 6, "dependency-error");
        }

        yield return Case("FUNC010-USR", 1, "success");
        yield return Case("FUNC010-USR", 2, "wrong-current-password");
        yield return Case("FUNC010-USR", 3, "empty-current-password");
        yield return Case("FUNC010-USR", 4, "not-found");
        yield return Case("FUNC010-USR", 5, "boundary");
        yield return Case("FUNC010-USR", 6, "dependency-error");
    }

    internal static async Task ExecuteCase(string testCaseId, string functionCode, string scenario)
    {
        Assert.StartsWith(functionCode + "-UTCID", testCaseId);

        switch (functionCode)
        {
            case "FUNC001-USR": await Register(scenario); break;
            case "FUNC002-USR": await Login(scenario, admin: false); break;
            case "FUNC003-USR": await Google(scenario); break;
            case "FUNC004-USR": await GitHub(scenario); break;
            case "FUNC005-USR": await VerifyEmail(scenario); break;
            case "FUNC006-USR": await ResendOtp(scenario); break;
            case "FUNC007-USR": await ForgotPassword(scenario); break;
            case "FUNC010-USR": await ChangePassword(scenario); break;
            case "FUNC016-USR": await Logout(scenario); break;
            case "FUNC083-ADM": await Login(scenario, admin: true); break;
            default: throw new Xunit.Sdk.XunitException($"No executable mapping for {functionCode}");
        }
    }

    internal static Task Execute(string testCaseId)
    {
        var row = Cases().Single(x => (string)x[0] == testCaseId);
        return ExecuteCase((string)row[0], (string)row[1], (string)row[2]);
    }

    private static object[] Case(string code, int number, string scenario) =>
        new object[] { $"{code}-UTCID{number:00}", code, scenario };

    private static async Task Register(string scenario)
    {
        var request = new RegisterRequest { Email = "candidate@devprep.vn", Password = "Existing@123", ConfirmPassword = "Existing@123", FullName = "Ho Thanh Duy" };
        if (scenario == "empty-email") request.Email = "";
        if (scenario == "empty-password") request.Password = "";
        if (scenario == "empty-name") request.FullName = "";
        if (scenario.StartsWith("empty-")) { AssertInvalid(request); return; }

        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        if (scenario == "success")
        {
            service.Setup(x => x.RegisterAsync(request)).ReturnsAsync(new RegisterResponse { Email = request.Email, Status = "Pending" });
            var result = Assert.IsType<ObjectResult>(await controller.Register(request));
            Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
            service.Verify(x => x.RegisterAsync(request), Times.Once);
            return;
        }

        var expected = scenario == "duplicate" ? new InvalidOperationException("duplicate email") : new Exception("dependency failure");
        service.Setup(x => x.RegisterAsync(request)).ThrowsAsync(expected);
        var actual = await Assert.ThrowsAsync(expected.GetType(), () => controller.Register(request));
        Assert.Equal(expected.Message, actual.Message);
    }

    private static async Task Login(string scenario, bool admin)
    {
        var request = new LoginRequest { Email = "candidate@devprep.vn", Password = "Existing@123" };
        if (scenario is "empty-email" or "all-empty" or "empty") request.Email = "";
        if (scenario is "empty-password" or "all-empty") request.Password = "";
        if (scenario is "empty-email" or "empty-password" or "all-empty" or "empty") { AssertInvalid(request); return; }

        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        Func<Task<IActionResult>> action = admin ? () => controller.AdminLogin(request) : () => controller.Login(request);
        if (scenario == "success")
        {
            var response = new LoginResponse { Email = request.Email, Role = admin ? "Admin" : "Candidate", AccessToken = "access", RefreshToken = "refresh" };
            if (admin) service.Setup(x => x.AdminLoginAsync(request)).ReturnsAsync(response);
            else service.Setup(x => x.LoginAsync(request)).ReturnsAsync(response);
            var ok = Assert.IsType<OkObjectResult>(await action());
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            if (admin) service.Verify(x => x.AdminLoginAsync(request), Times.Once);
            else service.Verify(x => x.LoginAsync(request), Times.Once);
            return;
        }

        var error = scenario switch { "boundary" => new ArgumentOutOfRangeException("credentials"), "dependency-error" => new Exception("dependency failure"), _ => new UnauthorizedAccessException("login rejected") };
        if (admin) service.Setup(x => x.AdminLoginAsync(request)).ThrowsAsync(error);
        else service.Setup(x => x.LoginAsync(request)).ThrowsAsync(error);
        var actual = await Assert.ThrowsAsync(error.GetType(), action);
        Assert.Equal(error.Message, actual.Message);
    }

    private static async Task Google(string scenario)
    {
        var request = new GoogleLoginRequest { IdToken = scenario == "empty" ? "" : scenario == "invalid-format" ? "invalid-format" : new string('t', scenario == "boundary" ? 4097 : 24) };
        if (scenario == "empty") { AssertInvalid(request); return; }
        await TokenAction(scenario, request, (c, r) => c.GoogleSso(r), (s, r) => s.Setup(x => x.GoogleLoginAsync(r)), request.IdToken);
    }

    private static async Task GitHub(string scenario)
    {
        var request = new GitHubLoginRequest { Code = scenario == "empty" ? "" : scenario == "invalid-format" ? "invalid-format" : new string('c', scenario == "boundary" ? 4097 : 24) };
        if (scenario == "empty") { AssertInvalid(request); return; }
        await TokenAction(scenario, request, (c, r) => c.GitHubSso(r), (s, r) => s.Setup(x => x.GitHubLoginAsync(r)), request.Code);
    }

    private static async Task TokenAction<T>(string scenario, T request, Func<AuthController, T, Task<IActionResult>> action, Func<Mock<IAuthService>, T, Moq.Language.Flow.ISetup<IAuthService, Task<LoginResponse>>> setup, string identity) where T : class
    {
        var service = new Mock<IAuthService>();
        var controller = new AuthController(service.Object);
        var configured = setup(service, request);
        if (scenario == "success")
        {
            configured.ReturnsAsync(new LoginResponse { AccessToken = "access", RefreshToken = "refresh" });
            Assert.IsType<OkObjectResult>(await action(controller, request));
            return;
        }
        var error = Failure(scenario, identity);
        configured.ThrowsAsync(error);
        var actual = await Assert.ThrowsAsync(error.GetType(), () => action(controller, request));
        Assert.Equal(error.Message, actual.Message);
    }

    private static async Task VerifyEmail(string scenario)
    {
        var request = new VerifyEmailOtpRequest { Email = scenario == "empty" ? "" : scenario == "invalid-format" ? "invalid-format" : "candidate@devprep.vn", Otp = scenario == "boundary" ? "1" : "482913" };
        if (scenario is "empty" or "invalid-format" or "boundary") { AssertInvalid(request); return; }
        var service = new Mock<IAuthService>(); var controller = new AuthController(service.Object);
        if (scenario == "success") { service.Setup(x => x.VerifyEmailOtpAsync(request)).ReturnsAsync(new VerifyEmailOtpResponse { Email = request.Email, IsEmailVerified = true }); Assert.IsType<OkObjectResult>(await controller.VerifyEmail(request)); return; }
        var error = Failure(scenario, request.Email); service.Setup(x => x.VerifyEmailOtpAsync(request)).ThrowsAsync(error); await Assert.ThrowsAsync(error.GetType(), () => controller.VerifyEmail(request));
    }

    private static async Task ResendOtp(string scenario)
    {
        var request = new ResendEmailOtpRequest { Email = scenario == "empty" ? "" : scenario == "invalid-format" ? "invalid-format" : scenario == "boundary" ? new string('a', 257) : "candidate@devprep.vn" };
        if (scenario is "empty" or "invalid-format" or "boundary") { AssertInvalid(request); return; }
        var service = new Mock<IAuthService>(); var controller = new AuthController(service.Object);
        if (scenario == "success") { service.Setup(x => x.ResendEmailOtpAsync(request)).ReturnsAsync(new ResendEmailOtpResponse { Email = request.Email }); Assert.IsType<OkObjectResult>(await controller.ResendOtp(request)); return; }
        var error = Failure(scenario, request.Email); service.Setup(x => x.ResendEmailOtpAsync(request)).ThrowsAsync(error); await Assert.ThrowsAsync(error.GetType(), () => controller.ResendOtp(request));
    }

    private static async Task ForgotPassword(string scenario)
    {
        var request = new ForgotPasswordRequest { Email = scenario == "empty" ? "" : scenario == "invalid-format" ? "invalid-format" : scenario == "boundary" ? new string('a', 257) : "candidate@devprep.vn" };
        if (scenario is "empty" or "invalid-format" or "boundary") { AssertInvalid(request); return; }
        var service = new Mock<IAuthService>(); var controller = new AuthController(service.Object);
        if (scenario == "success" || scenario == "not-found")
        {
            var message = "If the account exists, a reset link was sent.";
            service.Setup(x => x.ForgotPasswordAsync(request)).ReturnsAsync(new ForgotPasswordResponse { Message = message });
            var ok = Assert.IsType<OkObjectResult>(await controller.ForgotPassword(request));
            var body = Assert.IsType<ApiResponse<ForgotPasswordResponse>>(ok.Value);
            Assert.Equal(message, body.Message);
            return;
        }
        var error = Failure(scenario, request.Email); service.Setup(x => x.ForgotPasswordAsync(request)).ThrowsAsync(error); await Assert.ThrowsAsync(error.GetType(), () => controller.ForgotPassword(request));
    }

    private static async Task ChangePassword(string scenario)
    {
        var newPassword = scenario == "boundary" ? "x" : "NewSecure@456";
        var request = new ChangePasswordRequest { CurrentPassword = scenario == "empty-current-password" ? "" : "Current@123", NewPassword = newPassword, ConfirmPassword = newPassword };
        if (scenario is "empty-current-password" or "boundary") { AssertInvalid(request); return; }
        var service = new Mock<IAuthService>(); var accountId = Guid.NewGuid(); var controller = Authenticated(service, accountId);
        if (scenario == "success") { service.Setup(x => x.ChangePasswordAsync(accountId, request)).ReturnsAsync(new ChangePasswordResponse { Message = "changed" }); Assert.IsType<OkObjectResult>(await controller.ChangePassword(request)); return; }
        var error = Failure(scenario, accountId.ToString()); service.Setup(x => x.ChangePasswordAsync(accountId, request)).ThrowsAsync(error); await Assert.ThrowsAsync(error.GetType(), () => controller.ChangePassword(request));
    }

    private static async Task Logout(string scenario)
    {
        var request = new LogoutRequest { RefreshToken = scenario == "empty" ? "" : scenario == "boundary" ? new string('r', 8193) : scenario == "invalid-format" ? "invalid-format" : "rt_valid_2026_001" };
        if (scenario == "empty") { AssertInvalid(request); return; }
        var service = new Mock<IAuthService>(); var controller = new AuthController(service.Object);
        if (scenario == "success") { service.Setup(x => x.LogoutAsync(request)).ReturnsAsync(new LogoutResponse { Message = "Logged out" }); Assert.IsType<OkObjectResult>(await controller.Logout(request)); return; }
        var error = Failure(scenario, request.RefreshToken); service.Setup(x => x.LogoutAsync(request)).ThrowsAsync(error); await Assert.ThrowsAsync(error.GetType(), () => controller.Logout(request));
    }

    private static Exception Failure(string scenario, string value) => scenario switch
    {
        "not-found" => new KeyNotFoundException($"Not found: {value}"),
        "dependency-error" => new Exception("dependency failure"),
        "boundary" => new ArgumentOutOfRangeException(nameof(value)),
        _ => new ArgumentException("invalid input")
    };

    private static void AssertInvalid(object model)
    {
        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);
        Assert.False(valid);
        Assert.NotEmpty(results);
    }

    private static AuthController Authenticated(Mock<IAuthService> service, Guid accountId) => new(service.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, accountId.ToString()) }))
            }
        }
    };
}
