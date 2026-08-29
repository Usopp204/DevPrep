using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class AdminUsersControllerTests
{
    [Fact]
    public async Task GetUsers_returns_paged_service_result()
    {
        var service = new Mock<IAdminUserService>(); var query = new AdminUserQueryDto { Keyword = "duy", Page = 2, PageSize = 20 };
        var expected = new PagedResult<AdminUserListItemDto> { PageIndex = 2, PageSize = 20, TotalCount = 1, Items = new() { new AdminUserListItemDto { Email = "duy@example.com" } } };
        service.Setup(x => x.GetUsersAsync(query)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminUsersController(service.Object).GetUsers(query));
        Assert.Same(expected, Assert.IsType<ApiResponse<PagedResult<AdminUserListItemDto>>>(ok.Value).Data);
        service.Verify(x => x.GetUsersAsync(query), Times.Once);
    }

    [Fact]
    public async Task GetDetail_returns_not_found_when_service_has_no_user()
    {
        var service = new Mock<IAdminUserService>(); var id = Guid.NewGuid();
        service.Setup(x => x.GetUserDetailAsync(id)).ReturnsAsync((AdminUserDetailDto?)null);
        Assert.IsType<NotFoundObjectResult>(await new AdminUsersController(service.Object).GetDetail(id));
    }

    [Fact]
    public async Task GetDetail_returns_user_when_found()
    {
        var service = new Mock<IAdminUserService>(); var id = Guid.NewGuid(); var expected = new AdminUserDetailDto { UserId = id, Status = "Active" };
        service.Setup(x => x.GetUserDetailAsync(id)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminUsersController(service.Object).GetDetail(id));
        Assert.Equal(id, Assert.IsType<ApiResponse<AdminUserDetailDto>>(ok.Value).Data!.UserId);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task UpdateStatus_maps_service_outcome(bool changed, int expectedStatus)
    {
        var service = new Mock<IAdminUserService>(); var id = Guid.NewGuid(); var adminId = Guid.NewGuid(); var dto = new AdminUpdateUserStatusDto { Status = "Blocked", Reason = "policy" };
        service.Setup(x => x.UpdateStatusAsync(id, dto, adminId)).ReturnsAsync(changed);
        var result = await Authenticated(new AdminUsersController(service.Object), adminId).UpdateStatus(id, dto);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        service.Verify(x => x.UpdateStatusAsync(id, dto, adminId), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_maps_dependency_exception_to_bad_request()
    {
        var service = new Mock<IAdminUserService>(); var id = Guid.NewGuid(); var dto = new AdminUpdateUserStatusDto { Status = "Blocked" };
        service.Setup(x => x.UpdateStatusAsync(id, dto, It.IsAny<Guid?>())).ThrowsAsync(new InvalidOperationException("cannot block admin"));
        var result = Assert.IsType<BadRequestObjectResult>(await Authenticated(new AdminUsersController(service.Object), Guid.NewGuid()).UpdateStatus(id, dto));
        var body = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.Contains("cannot block admin", body.Message);
        Assert.False(body.Success);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task UpdateRole_maps_service_outcome(bool changed, int expectedStatus)
    {
        var service = new Mock<IAdminUserService>(); var id = Guid.NewGuid(); var adminId = Guid.NewGuid(); var dto = new AdminUpdateUserRoleDto { Role = "Manager" };
        service.Setup(x => x.UpdateRoleAsync(id, dto, adminId)).ReturnsAsync(changed);
        var result = await Authenticated(new AdminUsersController(service.Object), adminId).UpdateRole(id, dto);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task AutoBlockInactiveUsers_returns_actual_block_count()
    {
        var service = new Mock<IAdminUserService>(); var expected = new AdminAutoBlockInactiveResultDto { InactiveDays = 30, BlockedCount = 3 };
        service.Setup(x => x.AutoBlockInactiveUsersAsync(30)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminUsersController(service.Object).AutoBlockInactiveUsers(30));
        Assert.Equal(3, Assert.IsType<ApiResponse<AdminAutoBlockInactiveResultDto>>(ok.Value).Data!.BlockedCount);
    }

    private static AdminUsersController Authenticated(AdminUsersController controller, Guid id)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) })) } };
        return controller;
    }
}
