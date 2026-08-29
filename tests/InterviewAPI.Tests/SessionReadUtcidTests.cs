using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs.AiService;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class InterviewCatalogUtcidTests
{
    [Fact(DisplayName="FUNC033-USR-UTCID01")] public async Task Returns_roles_and_technologies() { var (c,_,ai,_)=ReadTest.Create(); ai.Setup(x=>x.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AiCatalogResponse{Roles=["Backend"],Technologies=new(){["Backend"]=["C#"]}}); var ok=Assert.IsType<OkObjectResult>(await c.GetCatalog(default)); var data=Assert.IsType<InterviewAPI.DTOs.ApiResponse<AiCatalogResponse>>(ok.Value).Data!; Assert.Equal("Backend",Assert.Single(data.Roles)); Assert.Equal("C#",Assert.Single(data.Technologies["Backend"])); }
    [Fact(DisplayName="FUNC033-USR-UTCID02")] public async Task Empty_catalog_is_valid() { var (c,_,ai,_)=ReadTest.Create(); ai.Setup(x=>x.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AiCatalogResponse()); Assert.Equal(200,ReadTest.Status(await c.GetCatalog(default))); }
    [Fact(DisplayName="FUNC033-USR-UTCID03")] public async Task Cancellation_token_is_forwarded() { var (c,_,ai,_)=ReadTest.Create(); using var source=new CancellationTokenSource(); ai.Setup(x=>x.GetCatalogAsync(source.Token)).ReturnsAsync(new AiCatalogResponse()); Assert.Equal(200,ReadTest.Status(await c.GetCatalog(source.Token))); ai.Verify(x=>x.GetCatalogAsync(source.Token),Times.Once); }
    [Fact(DisplayName="FUNC033-USR-UTCID04")] public async Task Ai_http_failure_returns_service_unavailable() { var (c,_,ai,_)=ReadTest.Create(); ai.Setup(x=>x.GetCatalogAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("offline")); Assert.Equal(503,ReadTest.Status(await c.GetCatalog(default))); }
    [Fact(DisplayName="FUNC033-USR-UTCID05")] public async Task Large_catalog_boundary_is_returned_without_truncation() { var (c,_,ai,_)=ReadTest.Create(); var roles=Enumerable.Range(1,1000).Select(x=>$"Role{x}").ToList(); ai.Setup(x=>x.GetCatalogAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AiCatalogResponse{Roles=roles}); var ok=Assert.IsType<OkObjectResult>(await c.GetCatalog(default)); Assert.Equal(1000,Assert.IsType<InterviewAPI.DTOs.ApiResponse<AiCatalogResponse>>(ok.Value).Data!.Roles.Count); }
    [Fact(DisplayName="FUNC033-USR-UTCID06")] public async Task Unexpected_dependency_error_returns_service_unavailable() { var (c,_,ai,_)=ReadTest.Create(); ai.Setup(x=>x.GetCatalogAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("ai")); Assert.Equal(503,ReadTest.Status(await c.GetCatalog(default))); }
}

public class ListUserSessionsUtcidTests
{
    [Fact(DisplayName="FUNC034-USR-UTCID01")] public async Task Returns_owned_session_items() { var (c,s,_,u)=ReadTest.Create(); var id=Guid.NewGuid(); s.Setup(x=>x.GetMySessionsAsync(u,It.IsAny<CancellationToken>())).ReturnsAsync([new SessionListItemDto{SessionId=id}]); var ok=Assert.IsType<OkObjectResult>(await c.GetMySessions(default)); Assert.Equal(id,Assert.Single(Assert.IsType<InterviewAPI.DTOs.ApiResponse<List<SessionListItemDto>>>(ok.Value).Data!).SessionId); }
    [Fact(DisplayName="FUNC034-USR-UTCID02")] public async Task Empty_session_list_is_valid() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetMySessionsAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync([]); Assert.Equal(200,ReadTest.Status(await c.GetMySessions(default))); }
    [Fact(DisplayName="FUNC034-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable_without_query() { var (c,s,_,_)=ReadTest.Create(false); Assert.Equal(503,ReadTest.Status(await c.GetMySessions(default))); s.Verify(x=>x.GetMySessionsAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never); }
    [Fact(DisplayName="FUNC034-USR-UTCID04")] public async Task Repository_failure_returns_service_unavailable() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetMySessionsAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db")); Assert.Equal(503,ReadTest.Status(await c.GetMySessions(default))); }
    [Fact(DisplayName="FUNC034-USR-UTCID05")] public async Task Large_result_boundary_is_not_truncated() { var (c,s,_,_)=ReadTest.Create(); var items=Enumerable.Range(0,1000).Select(_=>new SessionListItemDto{SessionId=Guid.NewGuid()}).ToList(); s.Setup(x=>x.GetMySessionsAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(items); var ok=Assert.IsType<OkObjectResult>(await c.GetMySessions(default)); Assert.Equal(1000,Assert.IsType<InterviewAPI.DTOs.ApiResponse<List<SessionListItemDto>>>(ok.Value).Data!.Count); }
    [Fact(DisplayName="FUNC034-USR-UTCID06")] public async Task Cancellation_failure_is_safely_mapped() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetMySessionsAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException()); Assert.Equal(503,ReadTest.Status(await c.GetMySessions(default))); }
}

public class GetTranscriptUtcidTests
{
    [Fact(DisplayName="FUNC035-USR-UTCID01")] public async Task Returns_owned_transcript() { var (c,s,_,u)=ReadTest.Create(); var id=Guid.NewGuid(); s.Setup(x=>x.GetTranscriptAsync(u,id,It.IsAny<CancellationToken>())).ReturnsAsync(new TranscriptResponse{SessionId=id,Questions=[new TranscriptQuestionDto()]}); var ok=Assert.IsType<OkObjectResult>(await c.GetTranscript(id,default)); Assert.Equal(id,Assert.IsType<InterviewAPI.DTOs.ApiResponse<TranscriptResponse>>(ok.Value).Data!.SessionId); }
    [Fact(DisplayName="FUNC035-USR-UTCID02")] public async Task Empty_transcript_is_valid() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetTranscriptAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new TranscriptResponse()); Assert.Equal(200,ReadTest.Status(await c.GetTranscript(Guid.NewGuid(),default))); }
    [Fact(DisplayName="FUNC035-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable_without_query() { var (c,s,_,_)=ReadTest.Create(false); Assert.Equal(503,ReadTest.Status(await c.GetTranscript(Guid.NewGuid(),default))); s.Verify(x=>x.GetTranscriptAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never); }
    [Fact(DisplayName="FUNC035-USR-UTCID04")] public async Task Missing_session_is_safely_mapped() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetTranscriptAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException()); Assert.Equal(503,ReadTest.Status(await c.GetTranscript(Guid.NewGuid(),default))); }
    [Fact(DisplayName="FUNC035-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetTranscriptAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync((Guid _,Guid id,CancellationToken _)=>new TranscriptResponse{SessionId=id}); Assert.Equal(200,ReadTest.Status(await c.GetTranscript(Guid.Empty,default))); Assert.Equal(200,ReadTest.Status(await c.GetTranscript(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),default))); }
    [Fact(DisplayName="FUNC035-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable() { var (c,s,_,_)=ReadTest.Create(); s.Setup(x=>x.GetTranscriptAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("db")); Assert.Equal(503,ReadTest.Status(await c.GetTranscript(Guid.NewGuid(),default))); }
}

internal static class ReadTest
{
    internal static (InterviewSessionController Controller,Mock<ISessionService> Service,Mock<IAiServiceClient> Ai,Guid UserId) Create(bool authenticated=true) { var s=new Mock<ISessionService>(); var ai=new Mock<IAiServiceClient>(); var u=Guid.NewGuid(); var ctx=new DefaultHttpContext(); if(authenticated) ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,u.ToString())])); var c=new InterviewSessionController(s.Object,ai.Object,new Mock<IPaymentApiClient>().Object,NullLogger<InterviewSessionController>.Instance){ControllerContext=new ControllerContext{HttpContext=ctx}}; return(c,s,ai,u); }
    internal static int? Status(IActionResult result)=>(result as ObjectResult)?.StatusCode;
}
