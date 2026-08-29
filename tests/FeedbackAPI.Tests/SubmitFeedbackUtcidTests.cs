using System.Security.Claims;
using FeedbackAPI.Clients;
using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FeedbackAPI.Tests;

public class SubmitSystemFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC075-USR-UTCID01")] public Task Success()=>SubmitFeedbackTest.System(1);
 [Fact(DisplayName="FUNC075-USR-UTCID02")] public Task Missing_identity()=>SubmitFeedbackTest.System(2);
 [Fact(DisplayName="FUNC075-USR-UTCID03")] public Task Invalid_feedback()=>SubmitFeedbackTest.System(3);
 [Fact(DisplayName="FUNC075-USR-UTCID04")] public Task Ineligible_user()=>SubmitFeedbackTest.System(4);
 [Fact(DisplayName="FUNC075-USR-UTCID05")] public Task Interview_timeout()=>SubmitFeedbackTest.System(5);
 [Fact(DisplayName="FUNC075-USR-UTCID06")] public Task Dependency_failure()=>SubmitFeedbackTest.System(6);
}

public class SubmitInterviewFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC076-USR-UTCID01")] public Task Success()=>SubmitFeedbackTest.Interview(1);
 [Fact(DisplayName="FUNC076-USR-UTCID02")] public Task Missing_identity()=>SubmitFeedbackTest.Interview(2);
 [Fact(DisplayName="FUNC076-USR-UTCID03")] public Task Invalid_feedback()=>SubmitFeedbackTest.Interview(3);
 [Fact(DisplayName="FUNC076-USR-UTCID04")] public Task Duplicate_feedback()=>SubmitFeedbackTest.Interview(4);
 [Fact(DisplayName="FUNC076-USR-UTCID05")] public Task Missing_session()=>SubmitFeedbackTest.Interview(5);
 [Fact(DisplayName="FUNC076-USR-UTCID06")] public Task Interview_timeout()=>SubmitFeedbackTest.Interview(6);
}

internal static class SubmitFeedbackTest
{
 private static (FeedbackController C,Mock<ISystemFeedbackService>S,Mock<IAiFeedbackService>A,Guid U) Create(bool auth){var s=new Mock<ISystemFeedbackService>();var a=new Mock<IAiFeedbackService>();var r=new Mock<IFeedbackReportService>();var u=Guid.NewGuid();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,u.ToString()),new Claim(ClaimTypes.Role,"User")]));ctx.Request.Headers.Authorization="Bearer test";return(new FeedbackController(s.Object,a.Object,r.Object){ControllerContext=new ControllerContext{HttpContext=ctx}},s,a,u);}
 internal static async Task System(int scenario){var(c,s,_,u)=Create(scenario!=2);var q=new SubmitSystemFeedbackRequest{OverallRating=5,UiUxRating=5,UsabilityRating=5,FeatureQualityRating=5};if(scenario==1)s.Setup(x=>x.SubmitAsync(u,q,"Bearer test",It.IsAny<CancellationToken>())).ReturnsAsync(new CreatedSystemFeedbackDto{FeedbackId=Guid.NewGuid(),OverallRating=5});if(scenario==3)s.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackValidationException("invalid"));if(scenario==4)s.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackEligibilityException("ineligible"));if(scenario==5)s.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiTimeoutException());if(scenario==6)s.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiDependencyException());var result=await c.SubmitSystem(q,default);Assert.Equal(scenario==1?201:scenario==2?401:scenario==3?400:scenario==4?409:503,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);}
 internal static async Task Interview(int scenario){var(c,_,a,u)=Create(scenario!=2);var q=new SubmitInterviewFeedbackRequest{SessionId=Guid.NewGuid(),OverallRating=5,QuestionQualityRating=5,AiAccuracyRating=5,ScoringFairnessRating=5,FeedbackUsefulnessRating=5};if(scenario==1)a.Setup(x=>x.SubmitAsync(u,q,"Bearer test",It.IsAny<CancellationToken>())).ReturnsAsync(new CreatedInterviewFeedbackDto{FeedbackId=Guid.NewGuid(),SessionId=q.SessionId});if(scenario==3)a.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackValidationException("invalid"));if(scenario==4)a.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new DuplicateFeedbackException());if(scenario==5)a.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiNotFoundException());if(scenario==6)a.Setup(x=>x.SubmitAsync(u,q,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiTimeoutException());var result=await c.SubmitInterview(q,default);Assert.Equal(scenario==1?201:scenario==2?401:scenario==3?400:scenario==4?409:scenario==5?404:503,Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);}
}
