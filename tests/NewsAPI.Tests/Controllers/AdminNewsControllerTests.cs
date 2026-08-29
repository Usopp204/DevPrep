using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NewsAPI.Controllers;
using NewsAPI.DTOs;
using NewsAPI.Services.Interfaces;
using Xunit;

namespace NewsAPI.Tests.Controllers;

public class AdminNewsControllerTests
{
    [Fact]
    public async Task GetList_forwards_query_and_returns_page()
    {
        var service = new Mock<IAdminNewsService>(); var query = new AdminNewsQueryDto { Keyword = "AI", PageIndex = 2, PageSize = 20 };
        var expected = new PagedResult<AdminNewsListItemDto> { Page = 2, PageSize = 20, TotalItems = 1 };
        service.Setup(x => x.GetListAsync(query)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new AdminNewsController(service.Object).GetList(query));
        Assert.Same(expected, Assert.IsType<ApiResponse<PagedResult<AdminNewsListItemDto>>>(ok.Value).Data);
    }

    [Fact]
    public async Task Filter_uses_filter_service_path()
    {
        var service = new Mock<IAdminNewsService>(); var query = new AdminNewsQueryDto { Status = "Published" };
        service.Setup(x => x.FilterAsync(query)).ReturnsAsync(new PagedResult<AdminNewsListItemDto>());
        Assert.IsType<OkObjectResult>(await new AdminNewsController(service.Object).Filter(query));
        service.Verify(x => x.FilterAsync(query), Times.Once);
        service.Verify(x => x.GetListAsync(It.IsAny<AdminNewsQueryDto>()), Times.Never);
    }

    [Theory]
    [InlineData(false, false, 200)]
    [InlineData(true, false, 404)]
    [InlineData(false, true, 404)]
    public async Task GetDetail_rejects_missing_or_soft_deleted_articles(bool missing, bool deleted, int expectedStatus)
    {
        var service = new Mock<IAdminNewsService>(); var id = Guid.NewGuid();
        service.Setup(x => x.GetDetailAsync(id)).ReturnsAsync(missing ? null : new AdminNewsDetailDto { NewsId = id, IsDeleted = deleted });
        var result = await new AdminNewsController(service.Object).GetDetail(id);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData("", "summary", "content")]
    [InlineData("title", "", "content")]
    [InlineData("title", "summary", "")]
    public void CreateDto_rejects_missing_required_content(string title, string summary, string content)
    {
        var dto = new AdminCreateNewsDto { Title = title, Slug = "slug", Summary = summary, Content = content };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task Create_uses_authenticated_author_and_returns_article()
    {
        var service = new Mock<IAdminNewsService>(); var author = Guid.NewGuid(); var dto = new AdminCreateNewsDto { Title = "Title", Slug = "slug", Summary = "Summary", Content = "Content" };
        var expected = new AdminNewsDetailDto { NewsId = Guid.NewGuid(), AuthorId = author, Title = dto.Title };
        service.Setup(x => x.CreateAsync(dto, author)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await Authenticated(new AdminNewsController(service.Object), author).Create(dto));
        Assert.Equal(author, Assert.IsType<ApiResponse<AdminNewsDetailDto>>(ok.Value).Data!.AuthorId);
        service.Verify(x => x.CreateAsync(dto, author), Times.Once);
    }

    [Fact]
    public async Task Create_maps_service_exception_to_bad_request()
    {
        var service = new Mock<IAdminNewsService>(); var dto = new AdminCreateNewsDto();
        service.Setup(x => x.CreateAsync(dto, It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("duplicate slug"));
        var result = Assert.IsType<BadRequestObjectResult>(await Authenticated(new AdminNewsController(service.Object), Guid.NewGuid()).Create(dto));
        Assert.Equal("duplicate slug", Assert.IsType<ApiResponse<object>>(result.Value).Message);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 404)]
    public async Task Delete_maps_service_outcome(bool deleted, int expectedStatus)
    {
        var service = new Mock<IAdminNewsService>(); var id = Guid.NewGuid(); var admin = Guid.NewGuid();
        service.Setup(x => x.DeleteAsync(id, admin)).ReturnsAsync(deleted);
        var result = await Authenticated(new AdminNewsController(service.Object), admin).Delete(id);
        Assert.Equal(expectedStatus, result is NoContentResult ? 204 : Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 400)]
    public async Task Restore_maps_service_outcome(bool restored, int expectedStatus)
    {
        var service = new Mock<IAdminNewsService>(); var id = Guid.NewGuid(); service.Setup(x => x.RestoreAsync(id)).ReturnsAsync(restored);
        var result = await new AdminNewsController(service.Object).Restore(id);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    private static AdminNewsController Authenticated(AdminNewsController controller, Guid id)
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) })) } };
        return controller;
    }
}
