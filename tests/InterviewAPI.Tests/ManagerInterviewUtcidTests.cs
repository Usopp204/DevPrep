using InterviewAPI.Controllers;
using InterviewAPI.DTOs;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static InterviewAPI.Tests.ManagerInterviewSupport;

namespace InterviewAPI.Tests;

public class ManagerInterviewListUtcidTests
{
 [Fact(DisplayName="FUNC177-MGR-UTCID01")] public async Task List_returns_page(){var(c,s)=New();var q=new InterviewQuery();var page=Page(Item());s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ReturnsAsync(page);Assert.Same(page,Data<PagedResult<ManagerInterviewSessionListItemDto>>(await c.GetList(q,default)));}
 [Fact(DisplayName="FUNC177-MGR-UTCID02")] public async Task List_returns_empty_page(){var(c,s)=New();var q=new InterviewQuery();s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ReturnsAsync(Page());Assert.Empty(Data<PagedResult<ManagerInterviewSessionListItemDto>>(await c.GetList(q,default)).Items);}
 [Fact(DisplayName="FUNC177-MGR-UTCID03")] public async Task List_forwards_pagination(){var(c,s)=New();var q=new InterviewQuery{Page=2,PageSize=50};s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ReturnsAsync(new PagedResult<ManagerInterviewSessionListItemDto>{Page=2,PageSize=50});var data=Data<PagedResult<ManagerInterviewSessionListItemDto>>(await c.GetList(q,default));Assert.Equal((2,50),(data.Page,data.PageSize));}
 [Fact(DisplayName="FUNC177-MGR-UTCID04")] public async Task List_forwards_invalid_pagination_for_normalization(){var(c,s)=New();var q=new InterviewQuery{Page=0,PageSize=0};s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ReturnsAsync(Page());_ =await c.GetList(q,default);s.VerifyAll();}
 [Fact(DisplayName="FUNC177-MGR-UTCID05")] public async Task List_forwards_cancellation_token(){var(c,s)=New();using var source=new CancellationTokenSource();var q=new InterviewQuery();s.Setup(x=>x.GetManagerSessionListAsync(q,source.Token)).ReturnsAsync(Page());_ =await c.GetList(q,source.Token);s.VerifyAll();}
 [Fact(DisplayName="FUNC177-MGR-UTCID06")] public void List_requires_manager_role(){var roles=Assert.Single(typeof(ManagerInterviewSessionsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles;Assert.Equal("Manager",roles);}
}

public class ManagerInterviewDetailUtcidTests
{
 [Fact(DisplayName="FUNC178-MGR-UTCID01")] public async Task Detail_returns_session_log(){var(c,s)=New();var log=Log();s.Setup(x=>x.GetManagerSessionLogAsync(log.SessionId,default)).ReturnsAsync(log);Assert.Same(log,Data<ManagerInterviewSessionLogDto>(await c.GetLog(log.SessionId,default)));}
 [Fact(DisplayName="FUNC178-MGR-UTCID02")] public async Task Detail_returns_identity_and_room_fields(){var(c,s)=New();var log=Log();log.UserEmail="user@example.com";log.ChannelName="room";s.Setup(x=>x.GetManagerSessionLogAsync(log.SessionId,default)).ReturnsAsync(log);var data=Data<ManagerInterviewSessionLogDto>(await c.GetLog(log.SessionId,default));Assert.Equal((log.UserId,"user@example.com","room"),(data.UserId,data.UserEmail,data.ChannelName));}
 [Fact(DisplayName="FUNC178-MGR-UTCID03")] public async Task Detail_returns_question_log(){var(c,s)=New();var log=Log();log.Questions=[new SessionQuestionDto()];s.Setup(x=>x.GetManagerSessionLogAsync(log.SessionId,default)).ReturnsAsync(log);Assert.Single(Data<ManagerInterviewSessionLogDto>(await c.GetLog(log.SessionId,default)).Questions);}
 [Fact(DisplayName="FUNC178-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){var(c,s)=New();s.Setup(x=>x.GetManagerSessionLogAsync(It.IsAny<Guid>(),default)).ReturnsAsync((ManagerInterviewSessionLogDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetLog(Guid.NewGuid(),default));}
 [Fact(DisplayName="FUNC178-MGR-UTCID05")] public async Task Detail_empty_guid_boundary_is_forwarded(){var(c,s)=New();s.Setup(x=>x.GetManagerSessionLogAsync(Guid.Empty,default)).ReturnsAsync((ManagerInterviewSessionLogDto?)null);Assert.IsType<NotFoundObjectResult>(await c.GetLog(Guid.Empty,default));s.VerifyAll();}
 [Fact(DisplayName="FUNC178-MGR-UTCID06")] public async Task Detail_propagates_failure(){var(c,s)=New();s.Setup(x=>x.GetManagerSessionLogAsync(It.IsAny<Guid>(),default)).ThrowsAsync(new TimeoutException());await Assert.ThrowsAsync<TimeoutException>(()=>c.GetLog(Guid.NewGuid(),default));}
}

public class ManagerSearchInterviewUtcidTests
{
 [Fact(DisplayName="FUNC179-MGR-UTCID01")] public Task Search_candidate_name()=>Search("Nguyen");
 [Fact(DisplayName="FUNC179-MGR-UTCID02")] public Task Search_email()=>Search("user@example.com");
 [Fact(DisplayName="FUNC179-MGR-UTCID03")] public Task Search_position_or_channel()=>Search("Backend");
 [Fact(DisplayName="FUNC179-MGR-UTCID04")] public Task Search_blank_keyword()=>Search(" ");
 [Fact(DisplayName="FUNC179-MGR-UTCID05")] public Task Search_long_boundary()=>Search(new string('x',1000));
 [Fact(DisplayName="FUNC179-MGR-UTCID06")] public async Task Search_propagates_failure(){var(c,s)=New();var q=new InterviewQuery{Keyword="fail"};s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ThrowsAsync(new InvalidOperationException());await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetList(q,default));}
 private static async Task Search(string keyword){var(c,s)=New();var q=new InterviewQuery{Keyword=keyword};s.Setup(x=>x.GetManagerSessionListAsync(q,default)).ReturnsAsync(Page());_ =await c.GetList(q,default);s.Verify(x=>x.GetManagerSessionListAsync(It.Is<InterviewQuery>(v=>v.Keyword==keyword),default),Times.Once);}
}

public class ManagerFilterInterviewUtcidTests
{
 [Fact(DisplayName="FUNC180-MGR-UTCID01")] public Task Filter_status_and_room()=>Filter(q=>{q.Status="Completed";q.RoomStatus="Closed";},q=>q.Status=="Completed"&&q.RoomStatus=="Closed");
 [Fact(DisplayName="FUNC180-MGR-UTCID02")] public Task Filter_level_and_mode()=>Filter(q=>{q.Level="Senior";q.InteractionMode="Video";},q=>q.Level=="Senior"&&q.InteractionMode=="Video");
 [Fact(DisplayName="FUNC180-MGR-UTCID03")] public Task Filter_question_type_and_user()=>Filter(q=>{q.QuestionType="Coding";q.UserId=Guid.NewGuid();},q=>q.QuestionType=="Coding"&&q.UserId.HasValue);
 [Fact(DisplayName="FUNC180-MGR-UTCID04")] public Task Filter_date_range()=>Filter(q=>{q.DateFrom=DateTime.UtcNow.AddDays(-1);q.DateTo=DateTime.UtcNow;},q=>q.DateFrom<=q.DateTo);
 [Fact(DisplayName="FUNC180-MGR-UTCID05")] public Task Filter_boundary_values()=>Filter(q=>{q.UserId=Guid.Empty;q.DateFrom=DateTime.MinValue;q.DateTo=DateTime.MaxValue;},q=>q.UserId==Guid.Empty&&q.DateFrom==DateTime.MinValue&&q.DateTo==DateTime.MaxValue);
 [Fact(DisplayName="FUNC180-MGR-UTCID06")] public Task Filter_deleted_flags()=>Filter(q=>{q.IncludeDeleted=true;q.OnlyDeleted=true;},q=>q.IncludeDeleted&&q.OnlyDeleted);
 private static async Task Filter(Action<InterviewQuery> set,Func<InterviewQuery,bool> match){var(c,s)=New();var q=new InterviewQuery();set(q);s.Setup(x=>x.GetManagerSessionListAsync(It.Is<InterviewQuery>(v=>match(v)),default)).ReturnsAsync(Page());_ =await c.GetList(q,default);s.VerifyAll();}
}

file static class ManagerInterviewSupport
{
 public static (ManagerInterviewSessionsController,Mock<IInterviewService>) New(){var service=new Mock<IInterviewService>();return(new ManagerInterviewSessionsController(service.Object),service);}
 public static ManagerInterviewSessionListItemDto Item()=>new(){SessionId=Guid.NewGuid(),UserId=Guid.NewGuid(),Level="Senior",InteractionMode="Text",QuestionType="Theory",Status="Completed",CreatedAt=DateTime.UtcNow};
 public static ManagerInterviewSessionLogDto Log()=>new(){SessionId=Guid.NewGuid(),UserId=Guid.NewGuid(),Level="Senior",InteractionMode="Text",QuestionType="Theory",Status="Completed",CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow};
 public static PagedResult<ManagerInterviewSessionListItemDto> Page(params ManagerInterviewSessionListItemDto[] items)=>new(){Items=items,TotalItems=items.Length,Page=1,PageSize=20};
 public static T Data<T>(IActionResult result)=>Assert.IsType<ApiResponse<T>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
}
