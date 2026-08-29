using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsAPI.Controllers;
using NewsAPI.DTOs;
using NewsAPI.Models;
using Xunit;
using static NewsAPI.Tests.ManagerNewsSupport;

namespace NewsAPI.Tests;

public class ManagerNewsListUtcidTests
{
 [Fact(DisplayName="FUNC173-MGR-UTCID01")] public async Task List_returns_active_articles_newest_first(){await using var db=Db();var old=Article("Old",false,"Published",DateTime.UtcNow.AddDays(-1));var latest=Article("Latest",false,"Published",DateTime.UtcNow);db.AddRange(old,latest);await db.SaveChangesAsync();Assert.Equal(new[]{latest.NewsId,old.NewsId},(await List(db)).Items.Select(x=>x.NewsId));}
 [Fact(DisplayName="FUNC173-MGR-UTCID02")] public async Task List_excludes_deleted_by_default(){await using var db=Db();db.AddRange(Article("Visible"),Article("Deleted",true));await db.SaveChangesAsync();Assert.Collection((await List(db)).Items,x=>Assert.Equal("Visible",x.Title));}
 [Fact(DisplayName="FUNC173-MGR-UTCID03")] public async Task List_returns_empty_page(){await using var db=Db();var data=await List(db);Assert.Empty(data.Items);Assert.Equal(0,data.TotalItems);}
 [Fact(DisplayName="FUNC173-MGR-UTCID04")] public async Task List_normalizes_nonpositive_page(){await using var db=Db();var data=await List(db,page:0);Assert.Equal(1,data.Page);}
 [Fact(DisplayName="FUNC173-MGR-UTCID05")] public async Task List_clamps_page_size_boundaries(){await using var db=Db();Assert.Equal(1,(await List(db,size:0)).PageSize);Assert.Equal(100,(await List(db,size:int.MaxValue)).PageSize);}
 [Fact(DisplayName="FUNC173-MGR-UTCID06")] public void List_authorizes_manager(){var roles=Assert.Single(typeof(ManagerNewsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles!;Assert.Contains("Manager",roles.Split(','));}
}

public class ManagerNewsDetailUtcidTests
{
 [Fact(DisplayName="FUNC174-MGR-UTCID01")] public async Task Detail_returns_full_content(){await using var db=Db();var item=Article("Title");item.Content="Full content";db.Add(item);await db.SaveChangesAsync();Assert.Equal("Full content",(await Detail(db,item.NewsId)).Content);}
 [Fact(DisplayName="FUNC174-MGR-UTCID02")] public async Task Detail_maps_category_and_author(){await using var db=Db();var category=Category("Engineering");var item=Article("Title");item.Category=category;item.CategoryId=category.CategoryId;db.AddRange(category,item);await db.SaveChangesAsync();var data=await Detail(db,item.NewsId);Assert.Equal("Engineering",data.CategoryName);Assert.Equal(item.AuthorId,data.AuthorId);}
 [Fact(DisplayName="FUNC174-MGR-UTCID03")] public async Task Detail_exposes_deleted_article(){await using var db=Db();var item=Article("Deleted",true);db.Add(item);await db.SaveChangesAsync();Assert.True((await Detail(db,item.NewsId)).IsDeleted);}
 [Fact(DisplayName="FUNC174-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){await using var db=Db();Assert.IsType<NotFoundObjectResult>(await new ManagerNewsController(db).GetDetail(Guid.NewGuid()));}
 [Fact(DisplayName="FUNC174-MGR-UTCID05")] public async Task Detail_empty_guid_boundary_returns_not_found(){await using var db=Db();Assert.IsType<NotFoundObjectResult>(await new ManagerNewsController(db).GetDetail(Guid.Empty));}
 [Fact(DisplayName="FUNC174-MGR-UTCID06")] public async Task Detail_preserves_view_count_boundary(){await using var db=Db();var item=Article("Popular");item.ViewCount=int.MaxValue;db.Add(item);await db.SaveChangesAsync();Assert.Equal(int.MaxValue,(await Detail(db,item.NewsId)).ViewCount);}
}

public class ManagerFilterNewsUtcidTests
{
 [Fact(DisplayName="FUNC175-MGR-UTCID01")] public Task Filter_published_status()=>Filter("Published",null,"Published");
 [Fact(DisplayName="FUNC175-MGR-UTCID02")] public Task Filter_draft_status()=>Filter("Draft",null,"Draft");
 [Fact(DisplayName="FUNC175-MGR-UTCID03")] public async Task Filter_deleted_status_returns_only_deleted(){await using var db=Db();db.AddRange(Article("Active"),Article("Deleted",true));await db.SaveChangesAsync();var item=Assert.Single((await List(db,status:" deleted ")).Items);Assert.True(item.IsDeleted);}
 [Fact(DisplayName="FUNC175-MGR-UTCID04")] public async Task Filter_unknown_status_returns_empty(){await using var db=Db();db.Add(Article("Article"));await db.SaveChangesAsync();Assert.Empty((await List(db,status:"Unknown")).Items);}
 [Fact(DisplayName="FUNC175-MGR-UTCID05")] public async Task Filter_empty_category_guid_boundary_returns_empty(){await using var db=Db();db.Add(Article("Article"));await db.SaveChangesAsync();Assert.Empty((await List(db,category:Guid.Empty)).Items);}
 [Fact(DisplayName="FUNC175-MGR-UTCID06")] public async Task Combined_status_and_category_filters_are_applied(){await using var db=Db();var category=Category("Tech");var match=Article("Match",false,"Published");match.Category=category;match.CategoryId=category.CategoryId;db.AddRange(category,match,Article("Draft",false,"Draft"));await db.SaveChangesAsync();Assert.Equal(match.NewsId,Assert.Single((await List(db,status:"Published",category:category.CategoryId)).Items).NewsId);}
 private static async Task Filter(string status,Guid? category,string expected){await using var db=Db();db.AddRange(Article("Published",false,"Published"),Article("Draft",false,"Draft"));await db.SaveChangesAsync();Assert.Equal(expected,Assert.Single((await List(db,status:status,category:category)).Items).Status);}
}

public class ManagerSearchNewsUtcidTests
{
 [Fact(DisplayName="FUNC176-MGR-UTCID01")] public Task Search_title()=>Search("Unique title",Article("Unique title"));
 [Fact(DisplayName="FUNC176-MGR-UTCID02")] public Task Search_slug(){var item=Article("Title");item.Slug="unique-slug";return Search("unique-slug",item);}
 [Fact(DisplayName="FUNC176-MGR-UTCID03")] public Task Search_summary(){var item=Article("Title");item.Summary="searchable summary";return Search("searchable",item);}
 [Fact(DisplayName="FUNC176-MGR-UTCID04")] public async Task Search_blank_behaves_as_no_filter(){await using var db=Db();db.AddRange(Article("One"),Article("Two"));await db.SaveChangesAsync();Assert.Equal(2,(await List(db,keyword:" ")).TotalItems);}
 [Fact(DisplayName="FUNC176-MGR-UTCID05")] public Task Search_trims_keyword_boundary()=>Search("  Needle  ",Article("Needle"));
 [Fact(DisplayName="FUNC176-MGR-UTCID06")] public async Task Search_no_match_returns_empty(){await using var db=Db();db.Add(Article("Article"));await db.SaveChangesAsync();Assert.Empty((await List(db,keyword:"missing")).Items);}
 private static async Task Search(string keyword,NewsArticle match){await using var db=Db();db.AddRange(match,Article("Other"));await db.SaveChangesAsync();Assert.Equal(match.NewsId,Assert.Single((await List(db,keyword:keyword)).Items).NewsId);}
}

file static class ManagerNewsSupport
{
 public static NewsDbContext Db()=>new(new DbContextOptionsBuilder<NewsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 public static NewsArticle Article(string title,bool deleted=false,string status="Published",DateTime? time=null){var now=time??DateTime.UtcNow;return new(){NewsId=Guid.NewGuid(),Title=title,Slug=Guid.NewGuid().ToString("N"),Summary="Summary",Content="Content",AuthorId=Guid.NewGuid(),Status=status,IsDeleted=deleted,CreatedAt=now,UpdatedAt=now,PublishedAt=status=="Published"?now:null};}
 public static NewsCategory Category(string name)=>new(){CategoryId=Guid.NewGuid(),Name=name,Slug=Guid.NewGuid().ToString("N"),CreatedAt=DateTime.UtcNow};
 public static async Task<PagedResult<ManagerNewsListItemDto>> List(NewsDbContext db,int page=1,int size=10,string? keyword=null,string? status=null,Guid? category=null)=>Assert.IsType<ApiResponse<PagedResult<ManagerNewsListItemDto>>>(Assert.IsType<OkObjectResult>(await new ManagerNewsController(db).GetList(page,size,keyword,status,category)).Value).Data!;
 public static async Task<ManagerNewsDetailDto> Detail(NewsDbContext db,Guid id)=>Assert.IsType<ApiResponse<ManagerNewsDetailDto>>(Assert.IsType<OkObjectResult>(await new ManagerNewsController(db).GetDetail(id)).Value).Data!;
}
