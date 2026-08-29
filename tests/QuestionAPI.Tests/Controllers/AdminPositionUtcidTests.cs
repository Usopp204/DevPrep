using Microsoft.AspNetCore.Mvc;
using Moq;
using QuestionAPI.Controllers;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;
using Xunit;

namespace QuestionAPI.Tests.Controllers;

public class AdminPositionUtcidTests
{
    [Fact(DisplayName = "FUNC145-ADM-UTCID01")]
    public async Task View_returns_page_from_service()
    {
        var (controller, service) = Create(); var query = new PositionQueryDto();
        var page = new PositionPagedResultDto { PageIndex = 1, PageSize = 10, TotalRecords = 1, Items = [Dto("Backend")] };
        service.Setup(x => x.GetPageAsync(query, default)).ReturnsAsync(page);
        Assert.Same(page, Data<PositionPagedResultDto>(await controller.Get(query, default)));
    }

    [Fact(DisplayName = "FUNC145-ADM-UTCID02")]
    public async Task View_forwards_search_status_and_pagination()
    {
        var (controller, service) = Create(); var query = new PositionQueryDto { PageIndex = 2, PageSize = 25, Search = " front ", Status = PositionStatusFilter.Deleted };
        service.Setup(x => x.GetPageAsync(query, default)).ReturnsAsync(new PositionPagedResultDto());
        _ = await controller.Get(query, default);
        service.Verify(x => x.GetPageAsync(It.Is<PositionQueryDto>(q => q.PageIndex == 2 && q.PageSize == 25 && q.Search == " front " && q.Status == PositionStatusFilter.Deleted), default), Times.Once);
    }

    [Fact(DisplayName = "FUNC145-ADM-UTCID03")]
    public async Task View_preserves_empty_page()
    {
        var (controller, service) = Create(); var query = new PositionQueryDto();
        service.Setup(x => x.GetPageAsync(query, default)).ReturnsAsync(new PositionPagedResultDto { Items = [], TotalRecords = 0 });
        var data = Data<PositionPagedResultDto>(await controller.Get(query, default));
        Assert.Empty(data.Items); Assert.Equal(0, data.TotalRecords);
    }

    [Fact(DisplayName = "FUNC145-ADM-UTCID04")]
    public async Task View_maps_invalid_query_to_bad_request()
    {
        var (controller, service) = Create(); var query = new PositionQueryDto { PageIndex = 0 };
        service.Setup(x => x.GetPageAsync(query, default)).ThrowsAsync(new PositionValidationException("PageIndex must be at least 1."));
        var response = Error(await controller.Get(query, default), 400);
        Assert.Contains("PageIndex", response.Message);
    }

    [Fact(DisplayName = "FUNC145-ADM-UTCID05")]
    public async Task View_forwards_boundary_page_size_one_hundred()
    {
        var (controller, service) = Create(); var query = new PositionQueryDto { PageIndex = int.MaxValue, PageSize = 100, Status = PositionStatusFilter.All };
        service.Setup(x => x.GetPageAsync(query, default)).ReturnsAsync(new PositionPagedResultDto());
        _ = await controller.Get(query, default);
        service.Verify(x => x.GetPageAsync(query, default), Times.Once);
    }

    [Fact(DisplayName = "FUNC145-ADM-UTCID06")]
    public async Task View_detail_returns_not_found_for_unknown_position()
    {
        var (controller, service) = Create(); var id = Guid.NewGuid();
        service.Setup(x => x.GetByIdAsync(id, default)).ReturnsAsync((PositionDto?)null);
        Error(await controller.GetById(id, default), 404);
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID01")]
    public async Task Create_returns_created_at_detail_route()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = "Backend" }; var dto = Dto("Backend");
        service.Setup(x => x.CreateAsync(request, default)).ReturnsAsync(dto);
        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(request, default));
        Assert.Equal(nameof(AdminPositionsController.GetById), result.ActionName); Assert.Equal(dto.PositionId, result.RouteValues!["id"]); Assert.Same(dto, Assert.IsType<ApiResponse<PositionDto>>(result.Value).Data);
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID02")]
    public async Task Create_forwards_name_unchanged()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = "  Data Engineer  " };
        service.Setup(x => x.CreateAsync(request, default)).ReturnsAsync(Dto("Data Engineer"));
        _ = await controller.Create(request, default); service.Verify(x => x.CreateAsync(request, default), Times.Once);
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID03")]
    public async Task Create_maps_duplicate_to_conflict()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = "Backend" };
        service.Setup(x => x.CreateAsync(request, default)).ThrowsAsync(new PositionDuplicateException("already exists"));
        Assert.Contains("already exists", Error(await controller.Create(request, default), 409).Message);
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID04")]
    public async Task Create_maps_blank_name_validation_to_bad_request()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = " " };
        service.Setup(x => x.CreateAsync(request, default)).ThrowsAsync(new PositionValidationException("Name is required."));
        Error(await controller.Create(request, default), 400);
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID05")]
    public async Task Create_accepts_fifty_character_boundary()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = new string('x', 50) };
        service.Setup(x => x.CreateAsync(request, default)).ReturnsAsync(Dto(request.Name));
        Assert.IsType<CreatedAtActionResult>(await controller.Create(request, default));
    }

    [Fact(DisplayName = "FUNC146-ADM-UTCID06")]
    public async Task Create_propagates_unexpected_dependency_failure()
    {
        var (controller, service) = Create(); var request = new CreatePositionDto { Name = "Backend" };
        service.Setup(x => x.CreateAsync(request, default)).ThrowsAsync(new TimeoutException());
        await Assert.ThrowsAsync<TimeoutException>(() => controller.Create(request, default));
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID01")]
    public async Task Update_returns_updated_position()
    {
        var (controller, service) = Create(); var id = Guid.NewGuid(); var request = new UpdatePositionDto { Name = "Platform" }; var dto = Dto("Platform", id);
        service.Setup(x => x.UpdateAsync(id, request, default)).ReturnsAsync(dto);
        Assert.Same(dto, Data<PositionDto>(await controller.Update(id, request, default)));
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID02")]
    public async Task Update_returns_not_found_when_service_returns_null()
    {
        var (controller, service) = Create(); var id = Guid.NewGuid(); var request = new UpdatePositionDto { Name = "Missing" };
        service.Setup(x => x.UpdateAsync(id, request, default)).ReturnsAsync((PositionDto?)null);
        Error(await controller.Update(id, request, default), 404);
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID03")]
    public async Task Update_maps_duplicate_to_conflict()
    {
        var (controller, service) = Create(); var request = new UpdatePositionDto { Name = "Taken" };
        service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), request, default)).ThrowsAsync(new PositionDuplicateException("duplicate"));
        Error(await controller.Update(Guid.NewGuid(), request, default), 409);
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID04")]
    public async Task Update_maps_deleted_or_invalid_position_to_bad_request()
    {
        var (controller, service) = Create(); var request = new UpdatePositionDto { Name = "New" };
        service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), request, default)).ThrowsAsync(new PositionValidationException("deleted"));
        Error(await controller.Update(Guid.NewGuid(), request, default), 400);
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID05")]
    public async Task Update_forwards_empty_guid_boundary()
    {
        var (controller, service) = Create(); var request = new UpdatePositionDto { Name = "New" };
        service.Setup(x => x.UpdateAsync(Guid.Empty, request, default)).ReturnsAsync((PositionDto?)null);
        Error(await controller.Update(Guid.Empty, request, default), 404); service.Verify(x => x.UpdateAsync(Guid.Empty, request, default), Times.Once);
    }

    [Fact(DisplayName = "FUNC147-ADM-UTCID06")]
    public async Task Update_propagates_unexpected_failure()
    {
        var (controller, service) = Create(); var request = new UpdatePositionDto { Name = "New" };
        service.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), request, default)).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Update(Guid.NewGuid(), request, default));
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID01")]
    public async Task Delete_returns_no_content_when_soft_delete_succeeds()
    {
        var (controller, service) = Create(); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id, default)).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await controller.Delete(id, default));
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID02")]
    public async Task Delete_returns_not_found_when_position_missing()
    {
        var (controller, _) = Create(); Error(await controller.Delete(Guid.NewGuid(), default), 404);
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID03")]
    public async Task Delete_returns_not_found_when_already_deleted()
    {
        var (controller, service) = Create(); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id, default)).ReturnsAsync(false);
        Error(await controller.Delete(id, default), 404);
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID04")]
    public async Task Delete_empty_guid_boundary_is_forwarded()
    {
        var (controller, service) = Create(); Error(await controller.Delete(Guid.Empty, default), 404); service.Verify(x => x.SoftDeleteAsync(Guid.Empty, default), Times.Once);
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID05")]
    public async Task Delete_honors_cancelled_request()
    {
        var (controller, service) = Create(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        service.Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), cancellation.Token)).ThrowsAsync(new OperationCanceledException(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.Delete(Guid.NewGuid(), cancellation.Token));
    }

    [Fact(DisplayName = "FUNC148-ADM-UTCID06")]
    public async Task Delete_propagates_dependency_failure()
    {
        var (controller, service) = Create(); service.Setup(x => x.SoftDeleteAsync(It.IsAny<Guid>(), default)).ThrowsAsync(new TimeoutException());
        await Assert.ThrowsAsync<TimeoutException>(() => controller.Delete(Guid.NewGuid(), default));
    }

    private static (AdminPositionsController Controller, Mock<IPositionService> Service) Create() { var service = new Mock<IPositionService>(); return (new AdminPositionsController(service.Object), service); }
    private static PositionDto Dto(string name, Guid? id = null) => new() { PositionId = id ?? Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private static T Data<T>(IActionResult result) => Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
    private static ApiResponse<object> Error(IActionResult result, int status) { var value = Assert.IsAssignableFrom<ObjectResult>(result); Assert.Equal(status, value.StatusCode); return Assert.IsType<ApiResponse<object>>(value.Value); }
}
