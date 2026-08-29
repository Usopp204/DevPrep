using InterviewAPI.Controllers;
using InterviewAPI.DTOs;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class FilterSessionsUtcidTests
{
 [Fact(DisplayName="FUNC066-USR-UTCID01")] public Task Status_filter()=>List(new InterviewQuery{Status="Completed"});
 [Fact(DisplayName="FUNC066-USR-UTCID02")] public Task Level_filter()=>List(new InterviewQuery{Level="Senior"});
 [Fact(DisplayName="FUNC066-USR-UTCID03")] public Task Mode_filter()=>List(new InterviewQuery{InteractionMode="Voice"});
 [Fact(DisplayName="FUNC066-USR-UTCID04")] public async Task Combined_filters(){var id=Guid.NewGuid();await List(new InterviewQuery{Status="Paused",Level="Junior",UserId=id,DateFrom=DateTime.UtcNow.AddDays(-1),DateTo=DateTime.UtcNow});}
 [Fact(DisplayName="FUNC066-USR-UTCID05")] public async Task Pagination_boundaries(){await List(new InterviewQuery{Page=1,PageSize=1});await List(new InterviewQuery{Page=int.MaxValue,PageSize=int.MaxValue});}
 [Fact(DisplayName="FUNC066-USR-UTCID06")] public async Task Dependency_failure_propagates(){var s=new Mock<IInterviewService>();var q=new InterviewQuery();s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>new AdminInterviewController(s.Object).GetList(q,default));}
 private static async Task List(InterviewQuery q){var s=new Mock<IInterviewService>();s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<InterviewListItemDto>{Page=q.Page,PageSize=q.PageSize});var body=Assert.IsType<ApiResponse<PagedResult<InterviewListItemDto>>>(Assert.IsType<OkObjectResult>(await new AdminInterviewController(s.Object).GetList(q,default)).Value);Assert.Equal(q.Page,body.Data!.Page);s.VerifyAll();}
}

public class ViewChatLogDetailsUtcidTests
{
 [Fact(DisplayName="FUNC068-USR-UTCID01")] public Task Existing_log()=>Log(Guid.NewGuid(),true);
 [Fact(DisplayName="FUNC068-USR-UTCID02")] public Task Missing_log()=>Log(Guid.NewGuid(),false);
 [Fact(DisplayName="FUNC068-USR-UTCID03")] public Task Empty_guid()=>Log(Guid.Empty,true);
 [Fact(DisplayName="FUNC068-USR-UTCID04")] public Task Maximum_guid()=>Log(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),true);
 [Fact(DisplayName="FUNC068-USR-UTCID05")] public async Task Empty_log_payload_is_valid(){var s=new Mock<IInterviewService>();var id=Guid.NewGuid();s.Setup(x=>x.GetManagerSessionLogAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(new ManagerInterviewSessionLogDto{SessionId=id});Assert.IsType<OkObjectResult>(await new ManagerInterviewSessionsController(s.Object).GetLog(id,default));}
 [Fact(DisplayName="FUNC068-USR-UTCID06")] public async Task Log_dependency_failure_propagates(){var s=new Mock<IInterviewService>();s.Setup(x=>x.GetManagerSessionLogAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>new ManagerInterviewSessionsController(s.Object).GetLog(Guid.NewGuid(),default));}
 private static async Task Log(Guid id,bool found){var s=new Mock<IInterviewService>();s.Setup(x=>x.GetManagerSessionLogAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(found?new ManagerInterviewSessionLogDto{SessionId=id}:null);var r=await new ManagerInterviewSessionsController(s.Object).GetLog(id,default);Assert.Equal(found?200:404,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
