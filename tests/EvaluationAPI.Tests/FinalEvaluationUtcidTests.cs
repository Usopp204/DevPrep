using EvaluationAPI.DTOs;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Clients;
using EvaluationAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EvaluationAPI.Tests;

public class FinalEvaluationUtcidTests
{
 [Theory][MemberData(nameof(Cases205))] public async Task Answer_evaluations(string id,int n){var(c,s,i)=EvaluateAnswerWorkbookTests.Create(n!=3);var sid=n==5?Guid.Empty:Guid.NewGuid();if(n==3){Assert.IsType<ObjectResult>(await c.GetAnswerEvaluations(sid,default));return;}if(n==2)i.Setup(x=>x.OwnsSessionAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(false);else if(n==4)i.Setup(x=>x.OwnsSessionAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiSessionNotFoundException());else{i.Setup(x=>x.OwnsSessionAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(true);s.Setup(x=>x.GetAnswerEvaluationsAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(n==6?[new EvaluateAnswerResponse{Score=10}]:[]);}var r=await c.GetAnswerEvaluations(sid,default);Assert.Equal(n is 2 or 4?403:200,(r as ObjectResult)!.StatusCode);Assert.StartsWith("FUNC205",id);}
 [Theory][MemberData(nameof(Cases206))] public async Task Existing_report(string id,int n){var(c,s,_)=EvaluateAnswerWorkbookTests.Create(n!=3);var sid=n==5?Guid.Empty:Guid.NewGuid();if(n==3){Assert.IsType<UnauthorizedObjectResult>(await c.GetPerformance(sid,default));return;}if(n==2)s.Setup(x=>x.GetPerformanceReportAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InterviewApiSessionNotFoundException());else if(n==4)s.Setup(x=>x.GetPerformanceReportAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new EvaluationDependencyException());else s.Setup(x=>x.GetPerformanceReportAsync(sid,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new PerformanceReportResponse{SessionId=sid,HasPersistedReport=n==6});var r=await c.GetPerformance(sid,default);Assert.Equal(n==2?404:n==4?503:200,(r as ObjectResult)!.StatusCode);Assert.StartsWith("FUNC206",id);}
 public static IEnumerable<object[]> Cases205()=>Cases("FUNC205-USR");public static IEnumerable<object[]> Cases206()=>Cases("FUNC206-USR");static IEnumerable<object[]> Cases(string f)=>Enumerable.Range(1,6).Select(i=>new object[]{$"{f}-UTCID{i:00}",i});
}
