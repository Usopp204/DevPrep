using System.Net;
using InterviewAPI.Clients;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class RefundCreditUtcidTests
{
    [Fact(DisplayName="FUNC052-USR-UTCID01")] public async Task Successful_refund_returns_balance(){var h=new Handler(HttpStatusCode.OK,"{\"success\":true,\"balanceAfter\":100,\"amountDeducted\":10}");var r=await Client(h).RefundCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"refund",default);Assert.True(r.Success);Assert.Equal(100,r.BalanceAfter);Assert.Equal("/api/internal/credit/refund",h.Path);}
    [Fact(DisplayName="FUNC052-USR-UTCID02")] public async Task Business_refusal_is_preserved(){var r=await Client(new Handler(HttpStatusCode.OK,"{\"success\":false,\"serviceAvailable\":true,\"message\":\"not refundable\"}")).RefundCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"refund",default);Assert.False(r.Success);Assert.True(r.ServiceAvailable);Assert.Equal("not refundable",r.Message);}
    [Fact(DisplayName="FUNC052-USR-UTCID03")] public async Task Empty_body_is_unavailable(){var r=await Client(new Handler(HttpStatusCode.OK,"")).RefundCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"refund",default);Assert.False(r.Success);Assert.False(r.ServiceAvailable);}
    [Fact(DisplayName="FUNC052-USR-UTCID04")] public async Task Http_error_is_unavailable(){var r=await Client(new Handler(HttpStatusCode.BadGateway,"down")).RefundCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"refund",default);Assert.False(r.Success);Assert.False(r.ServiceAvailable);Assert.Contains("BadGateway",r.Message);}
    [Fact(DisplayName="FUNC052-USR-UTCID05")] public async Task Amount_boundaries_are_serialized(){var h1=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Client(h1).RefundCreditAsync(Guid.Empty,0,Guid.Empty,"",default);Assert.Contains("\"amount\":0",h1.Body,StringComparison.OrdinalIgnoreCase);var h2=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Client(h2).RefundCreditAsync(Guid.NewGuid(),int.MaxValue,Guid.NewGuid(),"max",default);Assert.Contains(int.MaxValue.ToString(),h2.Body);}
    [Fact(DisplayName="FUNC052-USR-UTCID06")] public async Task Transport_exception_is_unavailable(){var r=await Client(new Thrower()).RefundCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"refund",default);Assert.False(r.Success);Assert.False(r.ServiceAvailable);Assert.Contains("offline",r.Message);}
    private static PaymentApiClient Client(HttpMessageHandler h)=>new(new HttpClient(h){BaseAddress=new Uri("http://payment")},NullLogger<PaymentApiClient>.Instance);
    private sealed class Handler(HttpStatusCode code,string response):HttpMessageHandler{public string? Path{get;private set;}public string Body{get;private set;}="";protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Path=request.RequestUri!.AbsolutePath;Body=request.Content is null?"":await request.Content.ReadAsStringAsync(ct);return new HttpResponseMessage(code){Content=new StringContent(response)};}}
    private sealed class Thrower:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>throw new HttpRequestException("offline");}
}

public class InitializeSessionUtcidTests
{
    [Fact(DisplayName="FUNC053-USR-UTCID01")] public async Task Initializes_configured_session(){var(c,s,_,_,u)=MediaTest.Create();var id=Guid.NewGuid();var req=new InitRequest{SessionId=id,Position="Backend"};s.Setup(x=>x.InitAsync(u,req,It.IsAny<CancellationToken>())).ReturnsAsync(new InitResponse{SessionId=id,TotalQuestions=5});var ok=Assert.IsType<OkObjectResult>(await c.Init(req,default));Assert.Equal(5,Assert.IsType<InterviewAPI.DTOs.ApiResponse<InitResponse>>(ok.Value).Data!.TotalQuestions);}
    [Fact(DisplayName="FUNC053-USR-UTCID02")] public async Task Invalid_request_returns_bad_request(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.InitAsync(It.IsAny<Guid>(),It.IsAny<InitRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("invalid"));Assert.IsType<BadRequestObjectResult>(await c.Init(new InitRequest(),default));}
    [Fact(DisplayName="FUNC053-USR-UTCID03")] public async Task Missing_identity_returns_bad_request_without_service_call(){var(c,s,_,_,_)=MediaTest.Create(false);Assert.IsType<BadRequestObjectResult>(await c.Init(new InitRequest(),default));s.Verify(x=>x.InitAsync(It.IsAny<Guid>(),It.IsAny<InitRequest>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact(DisplayName="FUNC053-USR-UTCID04")] public async Task Planning_dependency_failure_returns_service_unavailable(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.InitAsync(It.IsAny<Guid>(),It.IsAny<InitRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new SessionInitDependencyException());Assert.Equal(503,MediaTest.Status(await c.Init(new InitRequest(),default)));}
    [Fact(DisplayName="FUNC053-USR-UTCID05")] public async Task Session_id_boundaries_are_forwarded(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.InitAsync(It.IsAny<Guid>(),It.IsAny<InitRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync((Guid _,InitRequest r,CancellationToken _)=>new InitResponse{SessionId=r.SessionId});Assert.Equal(200,MediaTest.Status(await c.Init(new InitRequest{SessionId=Guid.Empty},default)));Assert.Equal(200,MediaTest.Status(await c.Init(new InitRequest{SessionId=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")},default)));}
    [Fact(DisplayName="FUNC053-USR-UTCID06")] public async Task Unexpected_failure_returns_bad_request_without_leaking_detail(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.InitAsync(It.IsAny<Guid>(),It.IsAny<InitRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("connection-secret"));var bad=Assert.IsType<BadRequestObjectResult>(await c.Init(new InitRequest(),default));Assert.DoesNotContain("connection-secret",bad.Value!.ToString());}
}
