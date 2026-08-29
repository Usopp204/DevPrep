using Microsoft.AspNetCore.Mvc;
using Moq;
using NewsAPI.Controllers;
using NewsAPI.DTOs;
using NewsAPI.Services.Interfaces;
using Xunit;

namespace NewsAPI.Tests.Controllers;

public class NewsControllerTests
{
    [Theory]
    [InlineData(null, null, 1, 9)]
    [InlineData("dotnet", null, 2, 20)]
    [InlineData("ai", "5c23dfb2-4206-4d74-9c06-3c352a40ec80", 1, 1)]
    public async Task GetList_forwards_search_filter_and_pagination(string? keyword, string? categoryText, int page, int pageSize)
    {
        var service = new Mock<INewsService>(); var category = categoryText is null ? (Guid?)null : Guid.Parse(categoryText);
        var query = new NewsQueryDto { Keyword = keyword, CategoryId = category, Page = page, PageSize = pageSize };
        var expected = new PagedResult<NewsListItemDto> { Page = page, PageSize = pageSize, TotalItems = 1, Items = new[] { new NewsListItemDto { Title = "News" } } };
        service.Setup(x => x.GetListAsync(query)).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new NewsController(service.Object).GetList(query));
        Assert.Same(expected, Assert.IsType<ApiResponse<PagedResult<NewsListItemDto>>>(ok.Value).Data);
        service.Verify(x => x.GetListAsync(query), Times.Once);
    }

    [Fact]
    public async Task GetList_propagates_service_failure()
    {
        var service = new Mock<INewsService>(); var query = new NewsQueryDto();
        service.Setup(x => x.GetListAsync(query)).ThrowsAsync(new InvalidOperationException("news unavailable"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new NewsController(service.Object).GetList(query));
        Assert.Equal("news unavailable", error.Message);
    }

    [Fact]
    public async Task GetCategories_returns_service_categories()
    {
        var service = new Mock<INewsService>(); var expected = new List<NewsCategoryDto> { new() { CategoryId = Guid.NewGuid(), Name = "Engineering" } };
        service.Setup(x => x.GetCategoriesAsync()).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await new NewsController(service.Object).GetCategories());
        Assert.Same(expected, Assert.IsType<ApiResponse<List<NewsCategoryDto>>>(ok.Value).Data);
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 404)]
    public async Task GetDetail_maps_found_and_missing_articles(bool found, int expectedStatus)
    {
        var service = new Mock<INewsService>(); var id = Guid.NewGuid();
        service.Setup(x => x.GetDetailAsync(id)).ReturnsAsync(found ? new NewsDetailDto { NewsId = id, Title = "Article" } : null);
        var result = await new NewsController(service.Object).GetDetail(id);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData("published-article", true, 200)]
    [InlineData("missing-article", false, 404)]
    public async Task GetDetailBySlug_maps_found_and_missing_articles(string slug, bool found, int expectedStatus)
    {
        var service = new Mock<INewsService>();
        service.Setup(x => x.GetDetailBySlugAsync(slug)).ReturnsAsync(found ? new NewsDetailDto { Slug = slug, Title = "Article" } : null);
        var result = await new NewsController(service.Object).GetDetailBySlug(slug);
        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        service.Verify(x => x.GetDetailBySlugAsync(slug), Times.Once);
    }
}
