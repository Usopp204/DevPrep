using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static FeedbackAPI.Tests.ManagerAiFeedbackSupport;

namespace FeedbackAPI.Tests;

public class ManagerAiFeedbackListUtcidTests
{
 [Fact(DisplayName="FUNC165-MGR-UTCID01")] public async Task List_returns_page(){var(c,s)=New();var q=new AiFeedbackQuery();var page=Page(Item());s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(page);Assert.Same(page,Data<PagedResult<AiFeedbackListItemDto>>(await c.GetList(q)));}
 [Fact(DisplayName="FUNC165-MGR-UTCID02")] public async Task List_preserves_empty_page(){var(c,s)=New();var q=new AiFeedbackQuery();s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());Assert.Empty(Data<PagedResult<AiFeedbackListItemDto>>(await c.GetList(q)).Items);}
 [Fact(DisplayName="FUNC165-MGR-UTCID03")] public async Task List_forwards_pagination(){var(c,s)=New();var q=new AiFeedbackQuery{Page=2,PageSize=50};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(new PagedResult<AiFeedbackListItemDto>{Page=2,PageSize=50});var data=Data<PagedResult<AiFeedbackListItemDto>>(await c.GetList(q));Assert.Equal((2,50),(data.Page,data.PageSize));}
 [Fact(DisplayName="FUNC165-MGR-UTCID04")] public async Task List_forwards_invalid_pagination_for_service_normalization(){var(c,s)=New();var q=new AiFeedbackQuery{Page=0,PageSize=0};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(new PagedResult<AiFeedbackListItemDto>{Page=1,PageSize=1});_ =await c.GetList(q);s.VerifyAll();}
 [Fact(DisplayName="FUNC165-MGR-UTCID05")] public async Task List_supports_integer_boundaries(){var(c,s)=New();var q=new AiFeedbackQuery{Page=int.MaxValue,PageSize=int.MaxValue};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());_ =await c.GetList(q);s.VerifyAll();}
 [Fact(DisplayName="FUNC165-MGR-UTCID06")] public void List_authorizes_manager(){var roles=Assert.Single(typeof(AdminAiFeedbackController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles!;Assert.Contains("Manager",roles.Split(','));}
}

public class ManagerAiFeedbackDetailUtcidTests
{
 [Fact(DisplayName="FUNC166-MGR-UTCID01")] public async Task Detail_returns_all_rating_fields(){var(c,s)=New();var item=Item();item.Rating=5;item.AiAccuracyRating=4;item.ScoringFairnessRating=3;s.Setup(x=>x.GetByIdAsync(item.Id)).ReturnsAsync(item);var data=Data<AiFeedbackListItemDto>(await c.GetById(item.Id));Assert.Equal((byte)5,data.Rating);Assert.Equal((byte)4,data.AiAccuracyRating);Assert.Equal((byte)3,data.ScoringFairnessRating);}
 [Fact(DisplayName="FUNC166-MGR-UTCID02")] public async Task Detail_returns_author_and_session_fields(){var(c,s)=New();var item=Item();item.UserFullName="Candidate";item.UserEmail="a@b.com";s.Setup(x=>x.GetByIdAsync(item.Id)).ReturnsAsync(item);var data=Data<AiFeedbackListItemDto>(await c.GetById(item.Id));Assert.Equal((item.UserId,item.SessionId,"Candidate","a@b.com"),(data.UserId,data.SessionId,data.UserFullName,data.UserEmail));}
 [Fact(DisplayName="FUNC166-MGR-UTCID03")] public async Task Detail_exposes_deleted_state(){var(c,s)=New();var item=Item();item.IsDeleted=true;s.Setup(x=>x.GetByIdAsync(item.Id)).ReturnsAsync(item);Assert.True(Data<AiFeedbackListItemDto>(await c.GetById(item.Id)).IsDeleted);}
 [Fact(DisplayName="FUNC166-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){var(c,s)=New();var id=Guid.NewGuid();s.Setup(x=>x.GetByIdAsync(id)).ReturnsAsync((AiFeedbackListItemDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetById(id));}
 [Fact(DisplayName="FUNC166-MGR-UTCID05")] public async Task Detail_empty_guid_boundary_is_forwarded(){var(c,s)=New();s.Setup(x=>x.GetByIdAsync(Guid.Empty)).ReturnsAsync((AiFeedbackListItemDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetById(Guid.Empty));s.VerifyAll();}
 [Fact(DisplayName="FUNC166-MGR-UTCID06")] public async Task Detail_propagates_dependency_failure(){var(c,s)=New();s.Setup(x=>x.GetByIdAsync(It.IsAny<Guid>())).ThrowsAsync(new TimeoutException());await Assert.ThrowsAsync<TimeoutException>(()=>c.GetById(Guid.NewGuid()));}
}

public class ManagerSearchAiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC167-MGR-UTCID01")] public Task Search_content()=>Search("helpful");
 [Fact(DisplayName="FUNC167-MGR-UTCID02")] public Task Search_candidate_name()=>Search("Nguyen");
 [Fact(DisplayName="FUNC167-MGR-UTCID03")] public Task Search_email()=>Search("user@example.com");
 [Fact(DisplayName="FUNC167-MGR-UTCID04")] public Task Search_blank_keyword()=>Search(" ");
 [Fact(DisplayName="FUNC167-MGR-UTCID05")] public Task Search_long_boundary()=>Search(new string('x',1000));
 [Fact(DisplayName="FUNC167-MGR-UTCID06")] public async Task Search_propagates_failure(){var(c,s)=New();var q=new AiFeedbackQuery{Keyword="fail"};s.Setup(x=>x.GetListAsync(q)).ThrowsAsync(new InvalidOperationException());await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetList(q));}
 private static async Task Search(string keyword){var(c,s)=New();var q=new AiFeedbackQuery{Keyword=keyword};s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(Page());_ =await c.GetList(q);s.Verify(x=>x.GetListAsync(It.Is<AiFeedbackQuery>(v=>v.Keyword==keyword)),Times.Once);}
}

public class ManagerFilterAiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC168-MGR-UTCID01")] public Task Filter_rating()=>Filter(q=>q.Rating=5,q=>q.Rating==5);
 [Fact(DisplayName="FUNC168-MGR-UTCID02")] public Task Filter_helpfulness()=>Filter(q=>q.IsHelpful=false,q=>q.IsHelpful==false);
 [Fact(DisplayName="FUNC168-MGR-UTCID03")] public Task Filter_user_and_session()=>Filter(q=>{q.UserId=Guid.NewGuid();q.SessionId=Guid.NewGuid();},q=>q.UserId.HasValue&&q.SessionId.HasValue);
 [Fact(DisplayName="FUNC168-MGR-UTCID04")] public Task Filter_date_range()=>Filter(q=>{q.DateFrom=DateTime.UtcNow.AddDays(-1);q.DateTo=DateTime.UtcNow;},q=>q.DateFrom<=q.DateTo);
 [Fact(DisplayName="FUNC168-MGR-UTCID05")] public Task Filter_rating_boundaries()=>Filter(q=>q.Rating=byte.MaxValue,q=>q.Rating==byte.MaxValue);
 [Fact(DisplayName="FUNC168-MGR-UTCID06")] public Task Filter_deleted_records()=>Filter(q=>{q.IncludeDeleted=true;q.OnlyDeleted=true;},q=>q.IncludeDeleted&&q.OnlyDeleted);
 private static async Task Filter(Action<AiFeedbackQuery> set,Func<AiFeedbackQuery,bool> match){var(c,s)=New();var q=new AiFeedbackQuery();set(q);s.Setup(x=>x.GetListAsync(It.Is<AiFeedbackQuery>(v=>match(v)))).ReturnsAsync(Page());_ =await c.GetList(q);s.VerifyAll();}
}

file static class ManagerAiFeedbackSupport
{
 public static (AdminAiFeedbackController,Mock<IAiFeedbackService>) New(){var service=new Mock<IAiFeedbackService>();return(new AdminAiFeedbackController(service.Object),service);}
 public static AiFeedbackListItemDto Item()=>new(){Id=Guid.NewGuid(),UserId=Guid.NewGuid(),SessionId=Guid.NewGuid(),Rating=4,Content="Feedback",CreatedAt=DateTime.UtcNow};
 public static PagedResult<AiFeedbackListItemDto> Page(params AiFeedbackListItemDto[] items)=>new(){Items=[..items],TotalItems=items.Length,Page=1,PageSize=20};
 public static T Data<T>(IActionResult result)=>Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
}
