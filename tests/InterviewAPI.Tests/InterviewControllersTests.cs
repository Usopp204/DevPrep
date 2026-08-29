using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class AdminInterviewControllerTests
{
    public static IEnumerable<object?[]> InvalidDateRanges()
    {
        yield return new object?[] { null, new DateOnly(2026, 1, 1) };
        yield return new object?[] { new DateOnly(2026, 1, 1), null };
        yield return new object?[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 1) };
        yield return new object?[] { new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 2) };
    }

    [Theory]
    [MemberData(nameof(InvalidDateRanges))]
    public async Task Dashboard_rejects_invalid_date_ranges(DateOnly? from, DateOnly? to)
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object);
        Assert.IsType<BadRequestObjectResult>(await controller.GetDashboardStatistics(from, to, default));
        service.Verify(x => x.GetDashboardStatisticsAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dashboard_returns_service_statistics()
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object); var from = new DateOnly(2026, 1, 1); var to = new DateOnly(2026, 1, 31); var expected = new InterviewDashboardStatisticsDto { TotalInterviewSessionsAsOf = 12 };
        service.Setup(x => x.GetDashboardStatisticsAsync(from, to, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetDashboardStatistics(from, to, default));
        Assert.Equal(12, Assert.IsType<ApiResponse<InterviewDashboardStatisticsDto>>(ok.Value).Data!.TotalInterviewSessionsAsOf);
    }

    [Fact]
    public async Task List_forwards_query_and_returns_page()
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object); var query = new InterviewQuery(); var expected = new PagedResult<InterviewListItemDto> { TotalItems = 3 };
        service.Setup(x => x.GetListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetList(query, default));
        Assert.Equal(3, Assert.IsType<ApiResponse<PagedResult<InterviewListItemDto>>>(ok.Value).Data!.TotalItems);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Detail_maps_found_state(bool found)
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object); var id = Guid.NewGuid();
        service.Setup(x => x.GetDetailAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(found ? new InterviewDetailDto { SessionId = id } : null);
        var result = await controller.GetDetail(id, default);
        if (found) Assert.Equal(id, Assert.IsType<ApiResponse<InterviewDetailDto>>(Assert.IsType<OkObjectResult>(result).Value).Data!.SessionId); else Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_maps_service_result(bool deleted)
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        var result = await controller.Delete(id, default); Assert.Equal(deleted ? 200 : 404, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Restore_maps_service_result(bool restored)
    {
        var service = new Mock<IInterviewService>(); var controller = new AdminInterviewController(service.Object); var id = Guid.NewGuid(); service.Setup(x => x.RestoreAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(restored);
        var result = await controller.Restore(id, default); Assert.Equal(restored ? 200 : 404, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }
}

public class ManagerInterviewSessionsControllerTests
{
    [Fact]
    public async Task List_returns_manager_page()
    {
        var service = new Mock<IInterviewService>(); var controller = new ManagerInterviewSessionsController(service.Object); var query = new InterviewQuery(); var expected = new PagedResult<ManagerInterviewSessionListItemDto> { TotalItems = 5 }; service.Setup(x => x.GetManagerSessionListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetList(query, default)); Assert.Equal(5, Assert.IsType<ApiResponse<PagedResult<ManagerInterviewSessionListItemDto>>>(ok.Value).Data!.TotalItems);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Log_maps_found_state(bool found)
    {
        var service = new Mock<IInterviewService>(); var controller = new ManagerInterviewSessionsController(service.Object); var id = Guid.NewGuid(); service.Setup(x => x.GetManagerSessionLogAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(found ? new ManagerInterviewSessionLogDto { SessionId = id } : null);
        var result = await controller.GetLog(id, default); if (found) Assert.Equal(id, Assert.IsType<ApiResponse<ManagerInterviewSessionLogDto>>(Assert.IsType<OkObjectResult>(result).Value).Data!.SessionId); else Assert.IsType<NotFoundObjectResult>(result);
    }
}

public class ProfileBiometricsControllerTests
{
    [Fact]
    public async Task Status_returns_enrollment_state_for_claimed_user()
    {
        var (controller, service, userId) = Create(); var expected = new BiometricStatusResponse { FaceEnrolled = true }; service.Setup(x => x.GetStatusAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetStatus(default)); Assert.True(Assert.IsType<ApiResponse<BiometricStatusResponse>>(ok.Value).Data!.FaceEnrolled);
    }

    [Fact]
    public async Task Status_maps_invalid_identity_claim_to_service_unavailable()
    {
        var (controller, service, _) = Create("invalid-guid"); Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.GetStatus(default)).StatusCode); service.Verify(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("face")]
    [InlineData("voice")]
    public async Task Enroll_returns_service_result_and_correct_modality(string modality)
    {
        var (controller, service, userId) = Create(); var file = new Mock<IFormFile>().Object; var expected = new BiometricEnrollResponse { Modality = modality, Dim = 128 };
        service.Setup(x => x.EnrollAsync(userId, It.IsAny<string>(), file, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var result = modality == "face" ? await controller.EnrollFace(file, default) : await controller.EnrollVoice(file, default);
        Assert.Equal(128, Assert.IsType<ApiResponse<BiometricEnrollResponse>>(Assert.IsType<OkObjectResult>(result).Value).Data!.Dim);
        service.Verify(x => x.EnrollAsync(userId, modality == "face" ? ProfileBiometricsService.ModalityFace : ProfileBiometricsService.ModalityVoice, file, It.IsAny<CancellationToken>()), Times.Once);
    }

    public static IEnumerable<object[]> EnrollmentErrors()
    {
        yield return new object[] { new BiometricQualityException("blurred"), 422 };
        yield return new object[] { new BiometricIdentityMismatchException("Face", .1, .8), 409 };
        yield return new object[] { new InvalidOperationException("dependency"), 503 };
    }

    [Theory]
    [MemberData(nameof(EnrollmentErrors))]
    public async Task Enroll_maps_typed_failures(Exception error, int status)
    {
        var (controller, service, _) = Create(); var file = new Mock<IFormFile>().Object; service.Setup(x => x.EnrollAsync(It.IsAny<Guid>(), It.IsAny<string>(), file, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await controller.EnrollFace(file, default)).StatusCode);
    }

    private static (ProfileBiometricsController Controller, Mock<IProfileBiometricsService> Service, Guid UserId) Create(string? claim = null)
    {
        var service = new Mock<IProfileBiometricsService>(); var userId = Guid.NewGuid(); var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, claim ?? userId.ToString()) })) };
        var controller = new ProfileBiometricsController(service.Object, NullLogger<ProfileBiometricsController>.Instance) { ControllerContext = new ControllerContext { HttpContext = context } };
        return (controller, service, userId);
    }
}
