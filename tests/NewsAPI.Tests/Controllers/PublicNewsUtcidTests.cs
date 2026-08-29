using Microsoft.AspNetCore.Mvc;
using Moq;
using NewsAPI.Controllers;
using NewsAPI.DTOs;
using NewsAPI.Services.Interfaces;
using Xunit;

namespace NewsAPI.Tests.Controllers;

public class ViewNewsUtcidTests
{
    [Fact(DisplayName="FUNC014-USR-UTCID01")] public async Task Default_page_returns_articles() => await PublicNewsTest.AssertList(new NewsQueryDto(), 1);
    [Fact(DisplayName="FUNC014-USR-UTCID02")] public async Task Empty_page_is_a_valid_result() => await PublicNewsTest.AssertList(new NewsQueryDto(), 0);
    [Fact(DisplayName="FUNC014-USR-UTCID03")] public async Task Category_filter_is_forwarded() { var id = Guid.NewGuid(); await PublicNewsTest.AssertList(new NewsQueryDto { CategoryId = id }, 1, q => Assert.Equal(id, q.CategoryId)); }
    [Fact(DisplayName="FUNC014-USR-UTCID04")] public async Task Pagination_boundaries_are_forwarded() { await PublicNewsTest.AssertList(new NewsQueryDto { Page = 1, PageSize = 1 }, 1); await PublicNewsTest.AssertList(new NewsQueryDto { Page = int.MaxValue, PageSize = int.MaxValue }, 0); }
    [Fact(DisplayName="FUNC014-USR-UTCID05")] public async Task Service_failure_is_propagated() { var (c, s) = PublicNewsTest.Create(); var q = new NewsQueryDto(); s.Setup(x => x.GetListAsync(q)).ThrowsAsync(new InvalidOperationException("unavailable")); Assert.Equal("unavailable", (await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetList(q))).Message); }
    [Fact(DisplayName="FUNC014-USR-UTCID06")] public async Task Keyword_is_preserved_verbatim() => await PublicNewsTest.AssertList(new NewsQueryDto { Keyword = "  .NET & AI  " }, 1, q => Assert.Equal("  .NET & AI  ", q.Keyword));
}

public class ViewNewsDetailsUtcidTests
{
    [Fact(DisplayName="FUNC015-USR-UTCID01")] public async Task Existing_id_returns_full_article() { var id = Guid.NewGuid(); var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailAsync(id)).ReturnsAsync(new NewsDetailDto { NewsId = id, Title = "Article", Content = "Full text", Slug = "article" }); var body = Assert.IsType<ApiResponse<NewsDetailDto>>(Assert.IsType<OkObjectResult>(await c.GetDetail(id)).Value); Assert.Equal("Full text", body.Data!.Content); }
    [Fact(DisplayName="FUNC015-USR-UTCID02")] public async Task Missing_id_returns_not_found() { var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync((NewsDetailDto?)null); Assert.IsType<NotFoundObjectResult>(await c.GetDetail(Guid.NewGuid())); }
    [Fact(DisplayName="FUNC015-USR-UTCID03")] public async Task Existing_slug_returns_article() { var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailBySlugAsync("article-slug")).ReturnsAsync(new NewsDetailDto { NewsId = Guid.NewGuid(), Title = "Article", Content = "Text", Slug = "article-slug" }); var body = Assert.IsType<ApiResponse<NewsDetailDto>>(Assert.IsType<OkObjectResult>(await c.GetDetailBySlug("article-slug")).Value); Assert.Equal("article-slug", body.Data!.Slug); }
    [Fact(DisplayName="FUNC015-USR-UTCID04")] public async Task Missing_slug_returns_not_found() { var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailBySlugAsync("missing")).ReturnsAsync((NewsDetailDto?)null); Assert.IsType<NotFoundObjectResult>(await c.GetDetailBySlug("missing")); }
    [Fact(DisplayName="FUNC015-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded() { foreach (var id in new[] { Guid.Empty, Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") }) { var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailAsync(id)).ReturnsAsync(new NewsDetailDto { NewsId = id, Title = "Boundary", Content = "Text", Slug = "boundary" }); Assert.IsType<OkObjectResult>(await c.GetDetail(id)); s.VerifyAll(); } }
    [Fact(DisplayName="FUNC015-USR-UTCID06")] public async Task Detail_failure_is_propagated() { var (c, s) = PublicNewsTest.Create(); s.Setup(x => x.GetDetailAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("database")); Assert.Equal("database", (await Assert.ThrowsAsync<InvalidOperationException>(() => c.GetDetail(Guid.NewGuid()))).Message); }
}

internal static class PublicNewsTest
{
    internal static (NewsController Controller, Mock<INewsService> Service) Create() { var service = new Mock<INewsService>(); return (new NewsController(service.Object), service); }
    internal static async Task AssertList(NewsQueryDto query, int itemCount, Action<NewsQueryDto>? verify = null)
    {
        var (controller, service) = Create(); var items = Enumerable.Range(0, itemCount).Select(i => new NewsListItemDto { NewsId = Guid.NewGuid(), Title = $"News {i}", Slug = $"news-{i}" }).ToArray();
        service.Setup(x => x.GetListAsync(query)).ReturnsAsync(new PagedResult<NewsListItemDto> { Items = items, Page = query.Page, PageSize = query.PageSize, TotalItems = itemCount });
        var body = Assert.IsType<ApiResponse<PagedResult<NewsListItemDto>>>(Assert.IsType<OkObjectResult>(await controller.GetList(query)).Value);
        Assert.Equal(itemCount, body.Data!.Items.Count()); verify?.Invoke(query); service.VerifyAll();
    }
}
