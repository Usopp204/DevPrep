using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;

namespace UserAPI.Tests.Controllers;

public class ViewSystemStatisticsUtcidTests
{
 [Fact(DisplayName="FUNC105-ADM-UTCID01")] public Task Current_month()=>StatsTest.Success(new DateOnly(2026,8,1),new DateOnly(2026,8,28),42);
 [Fact(DisplayName="FUNC105-ADM-UTCID02")] public Task Single_day()=>StatsTest.Success(new DateOnly(2026,8,28),new DateOnly(2026,8,28),1);
 [Fact(DisplayName="FUNC105-ADM-UTCID03")] public Task Empty_statistics()=>StatsTest.Success(new DateOnly(2026,1,1),new DateOnly(2026,1,31),0);
 [Fact(DisplayName="FUNC105-ADM-UTCID04")] public Task Missing_from()=>StatsTest.Bad(null,new DateOnly(2026,1,1));
 [Fact(DisplayName="FUNC105-ADM-UTCID05")] public Task Missing_to()=>StatsTest.Bad(new DateOnly(2026,1,1),null);
 [Fact(DisplayName="FUNC105-ADM-UTCID06")] public Task Dependency_failure()=>StatsTest.Dependency();
}
public class FilterSystemStatisticsUtcidTests
{
 [Fact(DisplayName="FUNC106-ADM-UTCID01")] public Task Valid_custom_range()=>StatsTest.Success(new DateOnly(2026,2,1),new DateOnly(2026,2,28),28);
 [Fact(DisplayName="FUNC106-ADM-UTCID02")] public Task Reversed_range()=>StatsTest.Bad(new DateOnly(2026,2,2),new DateOnly(2026,2,1));
 [Fact(DisplayName="FUNC106-ADM-UTCID03")] public Task Maximum_366_day_range()=>StatsTest.Success(new DateOnly(2024,1,1),new DateOnly(2024,12,31),366);
 [Fact(DisplayName="FUNC106-ADM-UTCID04")] public Task Overlong_range()=>StatsTest.Bad(new DateOnly(2024,1,1),new DateOnly(2025,1,1));
 [Fact(DisplayName="FUNC106-ADM-UTCID05")] public Task Bearer_header_is_forwarded()=>StatsTest.Authorization();
 [Fact(DisplayName="FUNC106-ADM-UTCID06")] public Task Cancellation_token_is_forwarded()=>StatsTest.Cancellation();
}
internal static class StatsTest
{
 private static (ManagementUserStatisticsController C,Mock<IAdminUserService>S) New(){var s=new Mock<IAdminUserService>();var c=new ManagementUserStatisticsController(s.Object){ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};return(c,s);}
 internal static async Task Success(DateOnly from,DateOnly to,long count){var(c,s)=New();s.Setup(x=>x.GetCandidateStatisticsAsync(from,to,"",It.IsAny<CancellationToken>())).ReturnsAsync(new CandidateStatisticsDto{From=from,To=to,TotalCandidatesAsOf=count});var body=Assert.IsType<ApiResponse<CandidateStatisticsDto>>(Assert.IsType<OkObjectResult>(await c.GetCandidateStatistics(from,to,default)).Value);Assert.Equal(count,body.Data!.TotalCandidatesAsOf);}
 internal static async Task Bad(DateOnly? from,DateOnly? to){var(c,s)=New();Assert.IsType<BadRequestObjectResult>(await c.GetCandidateStatistics(from,to,default));s.VerifyNoOtherCalls();}
 internal static async Task Dependency(){var(c,s)=New();var from=new DateOnly(2026,1,1);var to=new DateOnly(2026,1,2);s.Setup(x=>x.GetCandidateStatisticsAsync(from,to,It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());Assert.Equal(502,Assert.IsType<ObjectResult>(await c.GetCandidateStatistics(from,to,default)).StatusCode);}
 internal static async Task Authorization(){var(c,s)=New();c.Request.Headers.Authorization="Bearer token";var from=new DateOnly(2026,1,1);var to=from;s.Setup(x=>x.GetCandidateStatisticsAsync(from,to,"Bearer token",It.IsAny<CancellationToken>())).ReturnsAsync(new CandidateStatisticsDto());Assert.IsType<OkObjectResult>(await c.GetCandidateStatistics(from,to,default));s.VerifyAll();}
 internal static async Task Cancellation(){var(c,s)=New();var from=new DateOnly(2026,1,1);var to=from;using var source=new CancellationTokenSource();s.Setup(x=>x.GetCandidateStatisticsAsync(from,to,"",source.Token)).ReturnsAsync(new CandidateStatisticsDto());Assert.IsType<OkObjectResult>(await c.GetCandidateStatistics(from,to,source.Token));s.VerifyAll();}
}
