using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class ManagementUserStatisticsControllerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-02-02", "2026-02-01")]
    [InlineData("2025-01-01", "2026-01-02")]
    public async Task Statistics_rejects_missing_reversed_or_overlong_ranges(string? fromText, string? toText)
    {
        var service = new Mock<IAdminUserService>(); var controller = Create(service);
        DateOnly? from = fromText is null ? null : DateOnly.Parse(fromText); DateOnly? to = toText is null ? null : DateOnly.Parse(toText);
        Assert.IsType<BadRequestObjectResult>(await controller.GetCandidateStatistics(from, to, default));
        service.Verify(x => x.GetCandidateStatisticsAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Statistics_returns_data_and_forwards_authorization_header()
    {
        var service = new Mock<IAdminUserService>(); var from = new DateOnly(2026, 1, 1); var to = new DateOnly(2026, 1, 31); var expected = new CandidateStatisticsDto { From = from, To = to, TotalCandidatesAsOf = 42 };
        service.Setup(x => x.GetCandidateStatisticsAsync(from, to, "Bearer token", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = Create(service); controller.Request.Headers.Authorization = "Bearer token";
        var ok = Assert.IsType<OkObjectResult>(await controller.GetCandidateStatistics(from, to, default));
        Assert.Equal(42, Assert.IsType<ApiResponse<CandidateStatisticsDto>>(ok.Value).Data!.TotalCandidatesAsOf);
    }

    [Fact]
    public async Task Statistics_maps_downstream_http_failure_to_bad_gateway()
    {
        var service = new Mock<IAdminUserService>(); var from = new DateOnly(2026, 1, 1); var to = new DateOnly(2026, 1, 31);
        service.Setup(x => x.GetCandidateStatisticsAsync(from, to, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());
        var result = Assert.IsType<ObjectResult>(await Create(service).GetCandidateStatistics(from, to, default));
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
    }

    private static ManagementUserStatisticsController Create(Mock<IAdminUserService> service) => new(service.Object) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
}
