using EvaluationAPI.Clients;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EvaluationAPI.Tests;

public class ViewPerformanceReportUtcidTests
{
 [Fact(DisplayName="FUNC067-USR-UTCID01")] public async Task Scored_report_is_returned(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();var id=Guid.NewGuid();s.Setup(x=>x.GetPerformanceReportAsync(id,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new PerformanceReportResponse{SessionId=id,OverallScore=8});var ok=Assert.IsType<OkObjectResult>(await c.GetPerformance(id,default));Assert.Equal(8,Assert.IsType<EvaluationAPI.DTOs.ApiResponse<PerformanceReportResponse>>(ok.Value).Data!.OverallScore);}
 [Fact(DisplayName="FUNC067-USR-UTCID02")] public async Task Missing_identity_is_unauthorized(){var(c,s,_)=EvaluateAnswerWorkbookTests.Create(false);Assert.IsType<UnauthorizedObjectResult>(await c.GetPerformance(Guid.NewGuid(),default));s.VerifyNoOtherCalls();}
 [Fact(DisplayName="FUNC067-USR-UTCID03")] public Task Non_owner_is_forbidden()=>Error(new InterviewApiForbiddenException(),403);
 [Fact(DisplayName="FUNC067-USR-UTCID04")] public Task Missing_session_is_not_found()=>Error(new InterviewApiSessionNotFoundException(),404);
 [Fact(DisplayName="FUNC067-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded(){await Success(Guid.Empty);await Success(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));}
 [Fact(DisplayName="FUNC067-USR-UTCID06")] public Task Dependency_failure_is_unavailable()=>Error(new InterviewApiDependencyException(),503);
 private static async Task Success(Guid id){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.GetPerformanceReportAsync(id,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync(new PerformanceReportResponse{SessionId=id});Assert.Equal(200,EvaluateAnswerWorkbookTests.Status(await c.GetPerformance(id,default)));}
 private static async Task Error(Exception e,int status){var(c,s,_)=EvaluateAnswerWorkbookTests.Create();s.Setup(x=>x.GetPerformanceReportAsync(It.IsAny<Guid>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(e);Assert.Equal(status,EvaluateAnswerWorkbookTests.Status(await c.GetPerformance(Guid.NewGuid(),default)));}
}
