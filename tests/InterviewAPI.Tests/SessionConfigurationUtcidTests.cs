using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class ConfigureDurationUtcidTests
{
    [Fact(DisplayName="FUNC031-USR-UTCID01")] public async Task Success_returns_configured_duration() { var (c,s,u)=ConfigTest.Create(); var req=new ConfigDurationRequest{DurationMinutes=30}; s.Setup(x=>x.ConfigDurationAsync(u,req,It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse{UpdatedValue="30"}); Assert.Equal(200,ConfigTest.Status(await c.ConfigDuration(req,default))); }
    [Fact(DisplayName="FUNC031-USR-UTCID02")] public async Task Invalid_duration_returns_bad_request() { var (c,s,_)=ConfigTest.Create(); var req=new ConfigDurationRequest{DurationMinutes=17}; s.Setup(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),req,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Invalid duration.")); Assert.Equal(400,ConfigTest.Status(await c.ConfigDuration(req,default))); }
    [Fact(DisplayName="FUNC031-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable_without_service_call() { var (c,s,_)=ConfigTest.Create(false); Assert.Equal(503,ConfigTest.Status(await c.ConfigDuration(new ConfigDurationRequest(),default))); s.Verify(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),It.IsAny<ConfigDurationRequest>(),It.IsAny<CancellationToken>()),Times.Never); }
    [Fact(DisplayName="FUNC031-USR-UTCID04")] public async Task Active_session_guard_returns_conflict_and_retry_header() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),It.IsAny<ConfigDurationRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new SessionGuardException(409,"active_session_exists","active",12,Guid.NewGuid())); Assert.Equal(409,ConfigTest.Status(await c.ConfigDuration(new ConfigDurationRequest(),default))); Assert.Equal("12",c.Response.Headers.RetryAfter); }
    [Fact(DisplayName="FUNC031-USR-UTCID05")] public async Task Duration_boundaries_are_forwarded() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),It.IsAny<ConfigDurationRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse()); Assert.Equal(200,ConfigTest.Status(await c.ConfigDuration(new ConfigDurationRequest{DurationMinutes=15},default))); Assert.Equal(200,ConfigTest.Status(await c.ConfigDuration(new ConfigDurationRequest{DurationMinutes=60},default))); s.Verify(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),It.Is<ConfigDurationRequest>(r=>r.DurationMinutes==15||r.DurationMinutes==60),It.IsAny<CancellationToken>()),Times.Exactly(2)); }
    [Fact(DisplayName="FUNC031-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigDurationAsync(It.IsAny<Guid>(),It.IsAny<ConfigDurationRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("db")); Assert.Equal(503,ConfigTest.Status(await c.ConfigDuration(new ConfigDurationRequest(),default))); }
}

public class ConfigureLevelUtcidTests
{
    [Fact(DisplayName="FUNC032-USR-UTCID01")] public async Task Success_returns_configured_level() { var (c,s,u)=ConfigTest.Create(); var req=new ConfigLevelRequest{Level="Junior"}; s.Setup(x=>x.ConfigLevelAsync(u,req,It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse{UpdatedValue="Junior"}); Assert.Equal(200,ConfigTest.Status(await c.ConfigLevel(req,default))); }
    [Fact(DisplayName="FUNC032-USR-UTCID02")] public async Task Invalid_level_returns_bad_request() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigLevelAsync(It.IsAny<Guid>(),It.IsAny<ConfigLevelRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Invalid level.")); Assert.Equal(400,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest{Level="invalid-format"},default))); }
    [Fact(DisplayName="FUNC032-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable() { var (c,s,_)=ConfigTest.Create(false); Assert.Equal(503,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest(),default))); s.Verify(x=>x.ConfigLevelAsync(It.IsAny<Guid>(),It.IsAny<ConfigLevelRequest>(),It.IsAny<CancellationToken>()),Times.Never); }
    [Fact(DisplayName="FUNC032-USR-UTCID04")] public async Task Guard_failure_preserves_status_code() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigLevelAsync(It.IsAny<Guid>(),It.IsAny<ConfigLevelRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new SessionGuardException(409,"active_session_exists","active")); Assert.Equal(409,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest(),default))); }
    [Fact(DisplayName="FUNC032-USR-UTCID05")] public async Task Level_boundaries_are_forwarded() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigLevelAsync(It.IsAny<Guid>(),It.IsAny<ConfigLevelRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse()); Assert.Equal(200,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest{Level="Intern"},default))); Assert.Equal(200,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest{Level="Lead"},default))); }
    [Fact(DisplayName="FUNC032-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigLevelAsync(It.IsAny<Guid>(),It.IsAny<ConfigLevelRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("db")); Assert.Equal(503,ConfigTest.Status(await c.ConfigLevel(new ConfigLevelRequest(),default))); }
}

public class ConfigureInteractionUtcidTests
{
    [Fact(DisplayName="FUNC036-USR-UTCID01")] public async Task Success_returns_configured_mode() { var (c,s,u)=ConfigTest.Create(); var req=new ConfigInteractionRequest{Mode="Text"}; s.Setup(x=>x.ConfigInteractionAsync(u,req,It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse{UpdatedValue="Text"}); Assert.Equal(200,ConfigTest.Status(await c.ConfigInteraction(req,default))); }
    [Fact(DisplayName="FUNC036-USR-UTCID02")] public async Task Invalid_mode_returns_bad_request() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigInteractionAsync(It.IsAny<Guid>(),It.IsAny<ConfigInteractionRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Invalid interaction mode.")); Assert.Equal(400,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest{Mode="invalid-format"},default))); }
    [Fact(DisplayName="FUNC036-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable() { var (c,s,_)=ConfigTest.Create(false); Assert.Equal(503,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest(),default))); s.Verify(x=>x.ConfigInteractionAsync(It.IsAny<Guid>(),It.IsAny<ConfigInteractionRequest>(),It.IsAny<CancellationToken>()),Times.Never); }
    [Fact(DisplayName="FUNC036-USR-UTCID04")] public async Task Guard_failure_preserves_status_code() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigInteractionAsync(It.IsAny<Guid>(),It.IsAny<ConfigInteractionRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new SessionGuardException(409,"active_session_exists","active")); Assert.Equal(409,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest(),default))); }
    [Fact(DisplayName="FUNC036-USR-UTCID05")] public async Task Mode_boundaries_are_forwarded() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigInteractionAsync(It.IsAny<Guid>(),It.IsAny<ConfigInteractionRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigResponse()); Assert.Equal(200,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest{Mode="Text"},default))); Assert.Equal(200,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest{Mode="Voice"},default))); }
    [Fact(DisplayName="FUNC036-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable() { var (c,s,_)=ConfigTest.Create(); s.Setup(x=>x.ConfigInteractionAsync(It.IsAny<Guid>(),It.IsAny<ConfigInteractionRequest>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("db")); Assert.Equal(503,ConfigTest.Status(await c.ConfigInteraction(new ConfigInteractionRequest(),default))); }
}

internal static class ConfigTest
{
    internal static (InterviewSessionController Controller,Mock<ISessionService> Service,Guid UserId) Create(bool authenticated=true)
    {
        var service=new Mock<ISessionService>(); var user=Guid.NewGuid(); var context=new DefaultHttpContext(); if(authenticated) context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user.ToString())}));
        var controller=new InterviewSessionController(service.Object,new Mock<IAiServiceClient>().Object,new Mock<IPaymentApiClient>().Object,NullLogger<InterviewSessionController>.Instance){ControllerContext=new ControllerContext{HttpContext=context}};
        return(controller,service,user);
    }
    internal static int? Status(IActionResult result)=>(result as ObjectResult)?.StatusCode;
}
