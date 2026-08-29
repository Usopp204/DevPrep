using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FeedbackAPI.Tests;

public class FeedbackControllerTests
{
    [Fact]
    public async Task Admin_ai_list_returns_service_page()
    {
        var service = new Mock<IAiFeedbackService>(); var query = new AiFeedbackQuery(); var expected = new PagedResult<AiFeedbackListItemDto>(); service.Setup(x => x.GetListAsync(query)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminAiFeedbackController(service.Object).GetList(query));
        Assert.Same(expected, Assert.IsType<ApiResponse<PagedResult<AiFeedbackListItemDto>>>(ok.Value).Data);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Admin_ai_delete_maps_service_outcome(bool deleted, int status)
    {
        var service = new Mock<IAiFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id)).ReturnsAsync(deleted);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new AdminAiFeedbackController(service.Object).Delete(id)).StatusCode);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Admin_ai_restore_maps_service_outcome(bool restored, int status)
    {
        var service = new Mock<IAiFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.RestoreAsync(id)).ReturnsAsync(restored);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new AdminAiFeedbackController(service.Object).Restore(id)).StatusCode);
    }

    [Fact]
    public async Task Admin_ai_restore_maps_eligibility_conflict()
    {
        var service = new Mock<IAiFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.RestoreAsync(id)).ThrowsAsync(new FeedbackEligibilityException("not eligible"));
        var result = Assert.IsType<ConflictObjectResult>(await new AdminAiFeedbackController(service.Object).Restore(id));
        Assert.Equal("not eligible", Assert.IsType<ApiResponse<object>>(result.Value).Message);
    }

    [Fact]
    public async Task Admin_system_list_returns_service_page()
    {
        var service = new Mock<ISystemFeedbackService>(); var query = new SystemFeedbackQuery(); var expected = new PagedResult<SystemFeedbackListItemDto>(); service.Setup(x => x.GetListAsync(query)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminSystemFeedbackController(service.Object).GetList(query));
        Assert.Same(expected, Assert.IsType<ApiResponse<PagedResult<SystemFeedbackListItemDto>>>(ok.Value).Data);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Admin_system_delete_maps_service_outcome(bool deleted, int status)
    {
        var service = new Mock<ISystemFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id)).ReturnsAsync(deleted);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new AdminSystemFeedbackController(service.Object).Delete(id)).StatusCode);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Admin_system_restore_maps_service_outcome(bool restored, int status)
    {
        var service = new Mock<ISystemFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.RestoreAsync(id)).ReturnsAsync(restored);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new AdminSystemFeedbackController(service.Object).Restore(id)).StatusCode);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Delete_reply_maps_service_outcome(bool deleted, int status)
    {
        var service = new Mock<ISystemFeedbackService>(); var id = Guid.NewGuid(); var reply = Guid.NewGuid(); service.Setup(x => x.DeleteReplyAsync(id, reply)).ReturnsAsync(deleted);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new AdminSystemFeedbackController(service.Object).DeleteReply(id, reply)).StatusCode);
    }

    [Fact]
    public async Task Manager_list_returns_service_page()
    {
        var service = new Mock<ISystemFeedbackService>(); var query = new SystemFeedbackQuery(); service.Setup(x => x.GetListAsync(query)).ReturnsAsync(new PagedResult<SystemFeedbackListItemDto>());
        Assert.IsType<OkObjectResult>(await new ManagerFeedbackController(service.Object).GetList(query));
        service.Verify(x => x.GetListAsync(query), Times.Once);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Manager_detail_maps_found_and_missing(bool found, int status)
    {
        var service = new Mock<ISystemFeedbackService>(); var id = Guid.NewGuid(); service.Setup(x => x.GetDetailAsync(id)).ReturnsAsync(found ? new SystemFeedbackListItemDto() : null);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await new ManagerFeedbackController(service.Object).GetDetail(id)).StatusCode);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-02-02", "2026-02-01")]
    [InlineData("2025-01-01", "2026-01-02")]
    public async Task Statistics_rejects_invalid_date_ranges(string? fromText, string? toText)
    {
        var service = new Mock<IFeedbackStatisticsService>(); DateOnly? from = fromText is null ? null : DateOnly.Parse(fromText); DateOnly? to = toText is null ? null : DateOnly.Parse(toText);
        Assert.IsType<BadRequestObjectResult>(await new AdminFeedbackStatisticsController(service.Object).GetDashboardStatistics(from, to, default));
        service.Verify(x => x.GetDashboardStatisticsAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Statistics_returns_service_result()
    {
        var service = new Mock<IFeedbackStatisticsService>(); var from = new DateOnly(2026, 1, 1); var to = new DateOnly(2026, 1, 31); var expected = new FeedbackDashboardStatisticsDto(); service.Setup(x => x.GetDashboardStatisticsAsync(from, to, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminFeedbackStatisticsController(service.Object).GetDashboardStatistics(from, to, default));
        Assert.Same(expected, Assert.IsType<ApiResponse<FeedbackDashboardStatisticsDto>>(ok.Value).Data);
    }
}
