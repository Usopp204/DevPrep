using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static FeedbackAPI.Tests.ManagerSystemFeedbackSupport;

namespace FeedbackAPI.Tests;

public class ManagerSystemFeedbackListUtcidTests
{
 [Fact(DisplayName="FUNC169-MGR-UTCID01")] public async Task List_returns_page(){var(c,s)=New();var q=new SystemFeedbackQuery();var page=Page(Item());s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(page);Assert.Same(page,Data<PagedResult<SystemFeedbackListItemDto>>(await c.GetList(q)));}
 [Fact(DisplayName="FUNC169-MGR-UTCID02")] public async Task List_returns_empty_page(){var(c,s)=New();var q=new SystemFeedbackQuery();s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());Assert.Empty(Data<PagedResult<SystemFeedbackListItemDto>>(await c.GetList(q)).Items);}
 [Fact(DisplayName="FUNC169-MGR-UTCID03")] public async Task List_forwards_pagination(){var(c,s)=New();var q=new SystemFeedbackQuery{Page=3,PageSize=50};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(new PagedResult<SystemFeedbackListItemDto>{Page=3,PageSize=50});var data=Data<PagedResult<SystemFeedbackListItemDto>>(await c.GetList(q));Assert.Equal((3,50),(data.Page,data.PageSize));}
 [Fact(DisplayName="FUNC169-MGR-UTCID04")] public async Task List_forwards_invalid_pagination_to_service(){var(c,s)=New();var q=new SystemFeedbackQuery{Page=0,PageSize=0};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());_ =await c.GetList(q);s.VerifyAll();}
 [Fact(DisplayName="FUNC169-MGR-UTCID05")] public async Task List_supports_integer_boundaries(){var(c,s)=New();var q=new SystemFeedbackQuery{Page=int.MaxValue,PageSize=int.MaxValue};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());_ =await c.GetList(q);s.VerifyAll();}
 [Fact(DisplayName="FUNC169-MGR-UTCID06")] public void List_authorizes_manager(){var roles=Assert.Single(typeof(ManagerFeedbackController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles!;Assert.Contains("Manager",roles.Split(','));}
}

public class ManagerSystemFeedbackDetailUtcidTests
{
 [Fact(DisplayName="FUNC170-MGR-UTCID01")] public async Task Detail_returns_text_and_scores(){var(c,s)=New();var item=Item();item.Content="Full complaint";item.UiUxRating=4;s.Setup(x=>x.GetDetailAsync(item.Id)).ReturnsAsync(item);var data=Data<SystemFeedbackListItemDto>(await c.GetDetail(item.Id));Assert.Equal("Full complaint",data.Content);Assert.Equal((byte)4,data.UiUxRating);}
 [Fact(DisplayName="FUNC170-MGR-UTCID02")] public async Task Detail_returns_replies(){var(c,s)=New();var item=Item();item.ReplyCount=1;item.Replies=[new FeedbackReplyDto{ReplyId=Guid.NewGuid(),ReplyContent="Resolved"}];s.Setup(x=>x.GetDetailAsync(item.Id)).ReturnsAsync(item);Assert.Single(Data<SystemFeedbackListItemDto>(await c.GetDetail(item.Id)).Replies);}
 [Fact(DisplayName="FUNC170-MGR-UTCID03")] public async Task Detail_exposes_deleted_and_visibility_state(){var(c,s)=New();var item=Item();item.IsDeleted=true;item.IsVisible=false;s.Setup(x=>x.GetDetailAsync(item.Id)).ReturnsAsync(item);var data=Data<SystemFeedbackListItemDto>(await c.GetDetail(item.Id));Assert.True(data.IsDeleted);Assert.False(data.IsVisible);}
 [Fact(DisplayName="FUNC170-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){var(c,s)=New();s.Setup(x=>x.GetDetailAsync(It.IsAny<Guid>())).ReturnsAsync((SystemFeedbackListItemDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetDetail(Guid.NewGuid()));}
 [Fact(DisplayName="FUNC170-MGR-UTCID05")] public async Task Detail_empty_guid_is_forwarded(){var(c,s)=New();s.Setup(x=>x.GetDetailAsync(Guid.Empty)).ReturnsAsync((SystemFeedbackListItemDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetDetail(Guid.Empty));s.VerifyAll();}
 [Fact(DisplayName="FUNC170-MGR-UTCID06")] public async Task Detail_propagates_failure(){var(c,s)=New();s.Setup(x=>x.GetDetailAsync(It.IsAny<Guid>())).ThrowsAsync(new TimeoutException());await Assert.ThrowsAsync<TimeoutException>(()=>c.GetDetail(Guid.NewGuid()));}
}

public class ManagerSearchSystemFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC171-MGR-UTCID01")] public Task Search_subject()=>Search("login issue");
 [Fact(DisplayName="FUNC171-MGR-UTCID02")] public Task Search_content()=>Search("application crashes");
 [Fact(DisplayName="FUNC171-MGR-UTCID03")] public Task Search_unicode()=>Search("giao diện");
 [Fact(DisplayName="FUNC171-MGR-UTCID04")] public Task Search_blank()=>Search(" ");
 [Fact(DisplayName="FUNC171-MGR-UTCID05")] public Task Search_long_boundary()=>Search(new string('x',1000));
 [Fact(DisplayName="FUNC171-MGR-UTCID06")] public async Task Search_propagates_failure(){var(c,s)=New();var q=new SystemFeedbackQuery{Keyword="fail"};s.Setup(x=>x.GetListAsync(q)).ThrowsAsync(new InvalidOperationException());await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetList(q));}
 private static async Task Search(string keyword){var(c,s)=New();var q=new SystemFeedbackQuery{Keyword=keyword};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());_ =await c.GetList(q);s.Verify(x=>x.GetListAsync(It.Is<SystemFeedbackQuery>(v=>v.Keyword==keyword)),Times.Once);}
}

public class ManagerFilterSystemFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC172-MGR-UTCID01")] public Task Filter_category()=>Filter(q=>q.Category="Bug",q=>q.Category=="Bug");
 [Fact(DisplayName="FUNC172-MGR-UTCID02")] public Task Filter_status()=>Filter(q=>q.Status="Resolved",q=>q.Status=="Resolved");
 [Fact(DisplayName="FUNC172-MGR-UTCID03")] public Task Filter_rating_and_user()=>Filter(q=>{q.Rating=5;q.UserId=Guid.NewGuid();},q=>q.Rating==5&&q.UserId.HasValue);
 [Fact(DisplayName="FUNC172-MGR-UTCID04")] public Task Filter_date_range()=>Filter(q=>{q.DateFrom=DateTime.UtcNow.AddDays(-1);q.DateTo=DateTime.UtcNow;},q=>q.DateFrom<=q.DateTo);
 [Fact(DisplayName="FUNC172-MGR-UTCID05")] public Task Filter_boundary_values()=>Filter(q=>{q.Rating=byte.MaxValue;q.UserId=Guid.Empty;},q=>q.Rating==byte.MaxValue&&q.UserId==Guid.Empty);
 [Fact(DisplayName="FUNC172-MGR-UTCID06")] public Task Filter_deleted_flags()=>Filter(q=>{q.IncludeDeleted=true;q.OnlyDeleted=true;},q=>q.IncludeDeleted&&q.OnlyDeleted);
 private static async Task Filter(Action<SystemFeedbackQuery> set,Func<SystemFeedbackQuery,bool> match){var(c,s)=New();var q=new SystemFeedbackQuery();set(q);s.Setup(x=>x.GetListAsync(It.Is<SystemFeedbackQuery>(v=>match(v)))).ReturnsAsync(Page());_ =await c.GetList(q);s.VerifyAll();}
}

file static class ManagerSystemFeedbackSupport
{
 public static (ManagerFeedbackController,Mock<ISystemFeedbackService>) New(){var service=new Mock<ISystemFeedbackService>();return(new ManagerFeedbackController(service.Object),service);}
 public static SystemFeedbackListItemDto Item()=>new(){Id=Guid.NewGuid(),UserId=Guid.NewGuid(),Category="Bug",Subject="Subject",Content="Content",Status="New",IsVisible=true,CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow};
 public static PagedResult<SystemFeedbackListItemDto> Page(params SystemFeedbackListItemDto[] items)=>new(){Items=items,TotalItems=items.Length,Page=1,PageSize=20};
 public static T Data<T>(IActionResult result)=>Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
}
