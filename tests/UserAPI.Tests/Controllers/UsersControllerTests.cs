using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task GetMyProfile_returns_unauthorized_for_missing_account_claim()
    {
        var service = new Mock<IUserProfileService>();
        var controller = WithIdentity(new UsersController(service.Object), "invalid", "candidate@devprep.vn");
        Assert.IsType<UnauthorizedResult>(await controller.GetMyProfile());
        service.Verify(x => x.GetMyProfileAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetMyProfile_forwards_authenticated_identity_and_returns_profile()
    {
        var service = new Mock<IUserProfileService>(); var id = Guid.NewGuid();
        var expected = new ProfileResponse { Email = "candidate@devprep.vn", FullName = "Candidate" };
        service.Setup(x => x.GetMyProfileAsync(id, expected.Email)).ReturnsAsync(expected);
        var controller = WithIdentity(new UsersController(service.Object), id.ToString(), expected.Email);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetMyProfile());
        var body = Assert.IsType<ApiResponse<ProfileResponse>>(ok.Value);
        Assert.Same(expected, body.Data);
        service.Verify(x => x.GetMyProfileAsync(id, expected.Email), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData(".......................................................................................................................................................")]
    public void UpdateProfile_rejects_invalid_full_name_by_production_validation(string fullName)
    {
        var request = new UpdateProfileRequest { FullName = fullName };
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData("avatar.exe", 100L, "Avatar must be an image")]
    [InlineData("avatar.png", 0L, "Avatar file is empty")]
    [InlineData("avatar.png", 5242881L, "Avatar must not exceed 5 MB")]
    public async Task UpdateMyProfile_rejects_invalid_avatar_without_calling_service(string fileName, long length, string message)
    {
        var service = new Mock<IUserProfileService>();
        var file = new Mock<IFormFile>(); file.SetupGet(x => x.FileName).Returns(fileName); file.SetupGet(x => x.Length).Returns(length);
        var controller = WithIdentity(new UsersController(service.Object), Guid.NewGuid().ToString(), "candidate@devprep.vn");
        var result = Assert.IsType<BadRequestObjectResult>(await controller.UpdateMyProfile(new UpdateProfileRequest { FullName = "Candidate", Avatar = file.Object }));
        var body = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.Contains(message, body.Message);
        Assert.False(body.Success);
        service.Verify(x => x.UpdateMyProfileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateMyProfile_returns_updated_profile_and_forwards_avatar()
    {
        var service = new Mock<IUserProfileService>(); var id = Guid.NewGuid(); var email = "candidate@devprep.vn";
        var file = new Mock<IFormFile>(); file.SetupGet(x => x.FileName).Returns("avatar.webp"); file.SetupGet(x => x.Length).Returns(1024);
        var request = new UpdateProfileRequest { FullName = "Updated Candidate", Avatar = file.Object };
        var expected = new ProfileResponse { Email = email, FullName = request.FullName, AvatarUrl = "/avatars/a.webp" };
        service.Setup(x => x.UpdateMyProfileAsync(id, email, request.FullName, file.Object, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = WithIdentity(new UsersController(service.Object), id.ToString(), email);
        var ok = Assert.IsType<OkObjectResult>(await controller.UpdateMyProfile(request));
        var body = Assert.IsType<ApiResponse<ProfileResponse>>(ok.Value);
        Assert.Equal(expected.AvatarUrl, body.Data!.AvatarUrl);
        service.Verify(x => x.UpdateMyProfileAsync(id, email, request.FullName, file.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static T WithIdentity<T>(T controller, string id, string email) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Email, email) })) } };
        return controller;
    }
}
