using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class ViewProfileUtcidTests
{
    [Fact(DisplayName="FUNC008-USR-UTCID01")] public async Task Authenticated_user_gets_profile() => await ProfileTest.AssertGet("candidate@example.com", "Candidate");
    [Fact(DisplayName="FUNC008-USR-UTCID02")] public async Task Profile_without_optional_fields_is_returned() => await ProfileTest.AssertGet("candidate@example.com", null);
    [Fact(DisplayName="FUNC008-USR-UTCID03")] public async Task Missing_identity_is_unauthorized() { var (c, s, _) = ProfileTest.Create(null); Assert.IsType<UnauthorizedResult>(await c.GetMyProfile()); s.VerifyNoOtherCalls(); }
    [Fact(DisplayName="FUNC008-USR-UTCID04")] public async Task Invalid_identity_is_unauthorized() { var (c, s, _) = ProfileTest.Create("not-a-guid"); Assert.IsType<UnauthorizedResult>(await c.GetMyProfile()); s.VerifyNoOtherCalls(); }
    [Fact(DisplayName="FUNC008-USR-UTCID05")] public async Task Missing_email_defaults_to_empty() { var (c, s, id) = ProfileTest.Create(Guid.NewGuid().ToString(), null); s.Setup(x => x.GetMyProfileAsync(id, "")).ReturnsAsync(new ProfileResponse { Email = "" }); var body = Assert.IsType<ApiResponse<ProfileResponse>>(Assert.IsType<OkObjectResult>(await c.GetMyProfile()).Value); Assert.Equal("", body.Data!.Email); }
    [Fact(DisplayName="FUNC008-USR-UTCID06")] public async Task Profile_service_failure_is_propagated() { var (c, s, id) = ProfileTest.Create(Guid.NewGuid().ToString()); s.Setup(x => x.GetMyProfileAsync(id, It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("profile store")); Assert.Equal("profile store", (await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetMyProfile())).Message); }
}

public class UpdateProfileUtcidTests
{
    [Fact(DisplayName="FUNC009-USR-UTCID01")] public async Task Name_only_update_succeeds() => await ProfileTest.AssertUpdate("Candidate", null);
    [Fact(DisplayName="FUNC009-USR-UTCID02")] public async Task Image_update_succeeds() => await ProfileTest.AssertUpdate("Candidate", ProfileTest.File("avatar.png", 1024));
    [Fact(DisplayName="FUNC009-USR-UTCID03")] public async Task Empty_avatar_is_rejected() => await ProfileTest.AssertBadFile(ProfileTest.File("avatar.png", 0), "empty");
    [Fact(DisplayName="FUNC009-USR-UTCID04")] public async Task Oversized_avatar_is_rejected() => await ProfileTest.AssertBadFile(ProfileTest.File("avatar.jpg", 5 * 1024 * 1024 + 1L), "5 MB");
    [Fact(DisplayName="FUNC009-USR-UTCID05")] public async Task Non_image_avatar_is_rejected() => await ProfileTest.AssertBadFile(ProfileTest.File("avatar.exe", 10), "image");
    [Fact(DisplayName="FUNC009-USR-UTCID06")] public async Task Invalid_update_identity_is_unauthorized() { var (c, s, _) = ProfileTest.Create("invalid"); Assert.IsType<UnauthorizedResult>(await c.UpdateMyProfile(new UpdateProfileRequest { FullName = "Candidate" })); s.VerifyNoOtherCalls(); }
}

internal static class ProfileTest
{
    internal static (UsersController Controller, Mock<IUserProfileService> Service, Guid UserId) Create(string? identity = "valid", string? email = "candidate@example.com")
    {
        var service = new Mock<IUserProfileService>(); var id = identity == "valid" ? Guid.NewGuid() : Guid.TryParse(identity, out var parsed) ? parsed : Guid.Empty; var claims = new List<Claim>();
        if (identity is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, identity == "valid" ? id.ToString() : identity)); if (email is not null) claims.Add(new Claim(ClaimTypes.Email, email));
        var controller = new UsersController(service.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) } } }; return (controller, service, id);
    }
    internal static async Task AssertGet(string email, string? name) { var (c, s, id) = Create("valid", email); s.Setup(x => x.GetMyProfileAsync(id, email)).ReturnsAsync(new ProfileResponse { Email = email, FullName = name }); var body = Assert.IsType<ApiResponse<ProfileResponse>>(Assert.IsType<OkObjectResult>(await c.GetMyProfile()).Value); Assert.Equal(name, body.Data!.FullName); s.VerifyAll(); }
    internal static async Task AssertUpdate(string name, IFormFile? file) { var (c, s, id) = Create(); s.Setup(x => x.UpdateMyProfileAsync(id, "candidate@example.com", name, file, It.IsAny<CancellationToken>())).ReturnsAsync(new ProfileResponse { Email = "candidate@example.com", FullName = name, AvatarUrl = file is null ? null : "/avatar.png" }); var body = Assert.IsType<ApiResponse<ProfileResponse>>(Assert.IsType<OkObjectResult>(await c.UpdateMyProfile(new UpdateProfileRequest { FullName = name, Avatar = file })).Value); Assert.Equal(name, body.Data!.FullName); s.VerifyAll(); }
    internal static async Task AssertBadFile(IFormFile file, string message) { var (c, s, _) = Create(); var body = Assert.IsType<ApiResponse<object>>(Assert.IsType<BadRequestObjectResult>(await c.UpdateMyProfile(new UpdateProfileRequest { FullName = "Candidate", Avatar = file })).Value); Assert.Contains(message, body.Message, StringComparison.OrdinalIgnoreCase); s.VerifyNoOtherCalls(); }
    internal static IFormFile File(string name, long length) { var file = new Mock<IFormFile>(); file.SetupGet(x => x.FileName).Returns(name); file.SetupGet(x => x.Length).Returns(length); return file.Object; }
}
