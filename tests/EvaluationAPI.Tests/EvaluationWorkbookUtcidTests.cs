using System.Security.Claims;
using EvaluationAPI.Clients;
using EvaluationAPI.Controllers;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Services;
using EvaluationAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EvaluationAPI.Tests;

public class EvaluateAnswerWorkbookTests
{
 [Fact(DisplayName="FUNC061-USR-UTCID01")] public async Task Owned_answer_is_evaluated(){var(c,s,i)=Create();var session=Guid.NewGuid();var answer=Guid.NewGuid();var req=new EvaluateAnswerRequest{UserAnswer="answer"};i.Setup(x=>x.OwnsSessionAsync(session,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);s.Setup(x=>x.EvaluateAnswerAsync(session,answer,req,It.IsAny<CancellationToken>())).ReturnsAsync(new EvaluateAnswerResponse{Score=9});Assert.IsType<OkObjectResult>(await c.EvaluateAnswer(session,answer,req,default));}
 [Fact(DisplayName="FUNC061-USR-UTCID02")] public async Task Non_owner_is_forbidden_without_grading(){var(c,s,i)=Create();i.Setup(x=>x.OwnsSessionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(false);Assert.Equal(403,Status(await c.EvaluateAnswer(Guid.NewGuid(),Guid.NewGuid(),new EvaluateAnswerRequest(),default)));s.Verify(x=>x.EvaluateAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<EvaluateAnswerRequest>(),It.IsAny<CancellationToken>()),Times.Never);}
 [Fact(DisplayName="FUNC061-USR-UTCID03")] public async Task Invalid_request_returns_bad_request(){var(c,s,i)=Create();i.Setup(x=>x.OwnsSessionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);s.Setup(x=>x.EvaluateAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<EvaluateAnswerRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new EvaluationRequestException());Assert.IsType<BadRequestObjectResult>(await c.EvaluateAnswer(Guid.NewGuid(),Guid.NewGuid(),new EvaluateAnswerRequest(),default));}
 [Fact(DisplayName="FUNC061-USR-UTCID04")] public async Task Missing_session_is_forbidden(){var(c,_,i)=Create();i.Setup(x=>x.OwnsSessionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiSessionNotFoundException());Assert.Equal(403,Status(await c.EvaluateAnswer(Guid.NewGuid(),Guid.NewGuid(),new EvaluateAnswerRequest(),default)));}
 [Fact(DisplayName="FUNC061-USR-UTCID05")] public async Task Guid_and_score_boundaries_are_preserved(){var(c,s,i)=Create();i.Setup(x=>x.OwnsSessionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);s.Setup(x=>x.EvaluateAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<EvaluateAnswerRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EvaluateAnswerResponse{Score=10});Assert.Equal(200,Status(await c.EvaluateAnswer(Guid.Empty,Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),new EvaluateAnswerRequest(),default)));}
 [Fact(DisplayName="FUNC061-USR-UTCID06")] public async Task Evaluation_dependency_failure_returns_service_unavailable(){var(c,s,i)=Create();i.Setup(x=>x.OwnsSessionAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);s.Setup(x=>x.EvaluateAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<EvaluateAnswerRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new EvaluationDependencyException());Assert.Equal(503,Status(await c.EvaluateAnswer(Guid.NewGuid(),Guid.NewGuid(),new EvaluateAnswerRequest(),default)));}

 internal static (EvaluationController,Mock<IEvaluationService>,Mock<IInterviewApiClient>) Create(bool auth=true){var s=new Mock<IEvaluationService>();var i=new Mock<IInterviewApiClient>();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString())]));ctx.Request.Headers.Authorization="Bearer token";var a=new Mock<IHttpContextAccessor>();a.SetupGet(x=>x.HttpContext).Returns(ctx);return(new EvaluationController(s.Object,i.Object,a.Object){ControllerContext=new ControllerContext{HttpContext=ctx}},s,i);}
 internal static int? Status(IActionResult r)=>(r as ObjectResult)?.StatusCode;
}

public class GenerateReportWorkbookTests
{
 [Fact(DisplayName="FUNC062-USR-UTCID01")] public async Task Generates_and_returns_persisted_report(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();var id=Guid.NewGuid();s.Setup(x=>x.EnsurePerformanceReportAsync(id,"Bearer token",It.IsAny<CancellationToken>())).ReturnsAsync(new PerformanceReportResponse{SessionId=id,HasPersistedReport=true});Assert.Equal(200,EvaluateAnswerWorkbookTests.Status(await c.EnsureReport(id,default)));}
 [Fact(DisplayName="FUNC062-USR-UTCID02")] public async Task Missing_identity_is_unauthorized(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create(false);Assert.IsType<UnauthorizedObjectResult>(await c.EnsureReport(Guid.NewGuid(),default));s.Verify(x=>x.EnsurePerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);}
 [Fact(DisplayName="FUNC062-USR-UTCID03")] public async Task Non_owner_is_forbidden(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.EnsurePerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiForbiddenException());Assert.Equal(403,EvaluateAnswerWorkbookTests.Status(await c.EnsureReport(Guid.NewGuid(),default)));}
 [Fact(DisplayName="FUNC062-USR-UTCID04")] public async Task Report_not_ready_returns_conflict(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.EnsurePerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new AggregateReportNotReadyException("grading"));Assert.IsType<ConflictObjectResult>(await c.EnsureReport(Guid.NewGuid(),default));}
 [Fact(DisplayName="FUNC062-USR-UTCID05")] public async Task Session_guid_boundaries_are_forwarded(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.EnsurePerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((Guid id,string _,CancellationToken _)=>new PerformanceReportResponse{SessionId=id});Assert.Equal(200,EvaluateAnswerWorkbookTests.Status(await c.EnsureReport(Guid.Empty,default)));Assert.Equal(200,EvaluateAnswerWorkbookTests.Status(await c.EnsureReport(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),default)));}
 [Fact(DisplayName="FUNC062-USR-UTCID06")] public async Task Persistence_failure_returns_service_unavailable(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.EnsurePerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new EvaluationPersistenceException(new Exception("db")));Assert.Equal(503,EvaluateAnswerWorkbookTests.Status(await c.EnsureReport(Guid.NewGuid(),default)));}
}
