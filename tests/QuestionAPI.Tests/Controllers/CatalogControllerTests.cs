using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QuestionAPI.Controllers;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;
using Xunit;

namespace QuestionAPI.Tests.Controllers;

public class CatalogControllerTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Position_query_rejects_pagination_outside_boundaries(int page, int size)
    {
        var dto = new PositionQueryDto { PageIndex = page, PageSize = size };
        AssertInvalid(dto);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public void Position_name_rejects_empty_or_over_50_characters(string name) => AssertInvalid(new CreatePositionDto { Name = name });

    [Fact]
    public async Task Position_list_returns_page_and_forwards_query()
    {
        var service = new Mock<IPositionService>(); var query = new PositionQueryDto { Search = "backend" }; var expected = new PositionPagedResultDto { TotalRecords = 1 };
        service.Setup(x => x.GetPageAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminPositionsController(service.Object).Get(query, default));
        Assert.Same(expected, Assert.IsType<ApiResponse<PositionPagedResultDto>>(ok.Value).Data);
    }

    [Fact]
    public async Task Position_list_maps_validation_exception_to_bad_request()
    {
        var service = new Mock<IPositionService>(); var query = new PositionQueryDto();
        service.Setup(x => x.GetPageAsync(query, It.IsAny<CancellationToken>())).ThrowsAsync(new PositionValidationException("invalid page"));
        var result = Assert.IsType<BadRequestObjectResult>(await new AdminPositionsController(service.Object).Get(query, default));
        Assert.Equal("invalid page", Assert.IsType<ApiResponse<object>>(result.Value).Message);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Position_detail_maps_found_and_missing(bool found, int status)
    {
        var service = new Mock<IPositionService>(); var id = Guid.NewGuid(); service.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(found ? new PositionDto { PositionId = id } : null);
        var result = await new AdminPositionsController(service.Object).GetById(id, default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Position_create_returns_created_result()
    {
        var service = new Mock<IPositionService>(); var request = new CreatePositionDto { Name = "Backend" }; var expected = new PositionDto { PositionId = Guid.NewGuid(), Name = request.Name };
        service.Setup(x => x.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var result = Assert.IsType<CreatedAtActionResult>(await new AdminPositionsController(service.Object).Create(request, default));
        Assert.Equal(expected.PositionId, Assert.IsType<ApiResponse<PositionDto>>(result.Value).Data!.PositionId);
    }

    [Theory]
    [InlineData("validation", 400)]
    [InlineData("duplicate", 409)]
    public async Task Position_create_maps_business_exceptions(string kind, int status)
    {
        var service = new Mock<IPositionService>(); var request = new CreatePositionDto { Name = "Backend" }; Exception error = kind == "duplicate" ? new PositionDuplicateException("duplicate") : new PositionValidationException("invalid");
        service.Setup(x => x.CreateAsync(request, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        var result = await new AdminPositionsController(service.Object).Create(request, default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Position_delete_maps_service_outcome(bool deleted, int status)
    {
        var service = new Mock<IPositionService>(); var id = Guid.NewGuid(); service.Setup(x => x.SoftDeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);
        var result = await new AdminPositionsController(service.Object).Delete(id, default);
        Assert.Equal(status, result is NoContentResult ? 204 : Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Tech_stack_query_rejects_pagination_outside_boundaries(int page, int size) => AssertInvalid(new TechStackQueryDto { PageIndex = page, PageSize = size });

    [Theory]
    [InlineData("")]
    public void Tech_stack_name_is_required(string name) => AssertInvalid(new CreateTechStackDto { Name = name });

    [Fact]
    public async Task Tech_stack_list_returns_page()
    {
        var service = new Mock<ITechStackService>(); var query = new TechStackQueryDto { Search = "dotnet" }; var expected = new TechStackPagedResultDto { TotalRecords = 1 };
        service.Setup(x => x.GetPageAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminTechStacksController(service.Object).Get(query, default));
        Assert.Same(expected, Assert.IsType<ApiResponse<TechStackPagedResultDto>>(ok.Value).Data);
    }

    [Fact]
    public async Task Manager_tech_stack_list_uses_same_validated_service()
    {
        var service = new Mock<ITechStackService>(); var query = new TechStackQueryDto(); service.Setup(x => x.GetPageAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(new TechStackPagedResultDto());
        Assert.IsType<OkObjectResult>(await new ManagerTechStacksController(service.Object).Get(query, default));
        service.Verify(x => x.GetPageAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task Tech_stack_detail_maps_found_and_missing(bool found, int status)
    {
        var service = new Mock<ITechStackService>(); var id = Guid.NewGuid(); service.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(found ? new TechStackDto { TechStackId = id } : null);
        var result = await new AdminTechStacksController(service.Object).GetById(id, default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task Tech_stack_create_returns_created_result()
    {
        var service = new Mock<ITechStackService>(); var request = new CreateTechStackDto { Name = ".NET" }; var expected = new TechStackDto { TechStackId = Guid.NewGuid(), Name = request.Name };
        service.Setup(x => x.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var result = Assert.IsType<CreatedAtActionResult>(await new AdminTechStacksController(service.Object).Create(request, default));
        Assert.Equal(expected.TechStackId, Assert.IsType<ApiResponse<TechStackDto>>(result.Value).Data!.TechStackId);
    }

    [Theory]
    [InlineData("validation", 400)]
    [InlineData("duplicate", 409)]
    public async Task Tech_stack_create_maps_business_exceptions(string kind, int status)
    {
        var service = new Mock<ITechStackService>(); var request = new CreateTechStackDto { Name = ".NET" }; Exception error = kind == "duplicate" ? new TechStackDuplicateException("duplicate") : new TechStackValidationException("invalid");
        service.Setup(x => x.CreateAsync(request, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        var result = await new AdminTechStacksController(service.Object).Create(request, default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Tech_stack_deactivate_maps_service_outcome(bool changed, int status)
    {
        var service = new Mock<ITechStackService>(); var id = Guid.NewGuid(); service.Setup(x => x.DeactivateAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(changed);
        var result = await new AdminTechStacksController(service.Object).Deactivate(id, default);
        Assert.Equal(status, result is NoContentResult ? 204 : Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    private static void AssertInvalid(object dto)
    {
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true));
        Assert.NotEmpty(errors);
    }
}
