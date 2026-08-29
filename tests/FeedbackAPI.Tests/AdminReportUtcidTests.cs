using System.Security.Claims;
using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
namespace FeedbackAPI.Tests;
public class Func120ReportUtcidTests
{
 [Fact(DisplayName="FUNC120-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(120,1);
 [Fact(DisplayName="FUNC120-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(120,2);
 [Fact(DisplayName="FUNC120-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(120,3);
 [Fact(DisplayName="FUNC120-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(120,4);
 [Fact(DisplayName="FUNC120-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(120,5);
 [Fact(DisplayName="FUNC120-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(120,6);
}
public class Func121ReportUtcidTests
{
 [Fact(DisplayName="FUNC121-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(121,1);
 [Fact(DisplayName="FUNC121-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(121,2);
 [Fact(DisplayName="FUNC121-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(121,3);
 [Fact(DisplayName="FUNC121-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(121,4);
 [Fact(DisplayName="FUNC121-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(121,5);
 [Fact(DisplayName="FUNC121-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(121,6);
}
public class Func122ReportUtcidTests
{
 [Fact(DisplayName="FUNC122-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(122,1);
 [Fact(DisplayName="FUNC122-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(122,2);
 [Fact(DisplayName="FUNC122-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(122,3);
 [Fact(DisplayName="FUNC122-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(122,4);
 [Fact(DisplayName="FUNC122-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(122,5);
 [Fact(DisplayName="FUNC122-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(122,6);
}
public class Func123ReportUtcidTests
{
 [Fact(DisplayName="FUNC123-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(123,1);
 [Fact(DisplayName="FUNC123-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(123,2);
 [Fact(DisplayName="FUNC123-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(123,3);
 [Fact(DisplayName="FUNC123-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(123,4);
 [Fact(DisplayName="FUNC123-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(123,5);
 [Fact(DisplayName="FUNC123-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(123,6);
}
public class Func124ReportUtcidTests
{
 [Fact(DisplayName="FUNC124-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(124,1);
 [Fact(DisplayName="FUNC124-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(124,2);
 [Fact(DisplayName="FUNC124-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(124,3);
 [Fact(DisplayName="FUNC124-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(124,4);
 [Fact(DisplayName="FUNC124-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(124,5);
 [Fact(DisplayName="FUNC124-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(124,6);
}
public class Func125ReportUtcidTests
{
 [Fact(DisplayName="FUNC125-ADM-UTCID01")] public Task Case1()=>AdminReportUtcid.Run(125,1);
 [Fact(DisplayName="FUNC125-ADM-UTCID02")] public Task Case2()=>AdminReportUtcid.Run(125,2);
 [Fact(DisplayName="FUNC125-ADM-UTCID03")] public Task Case3()=>AdminReportUtcid.Run(125,3);
 [Fact(DisplayName="FUNC125-ADM-UTCID04")] public Task Case4()=>AdminReportUtcid.Run(125,4);
 [Fact(DisplayName="FUNC125-ADM-UTCID05")] public Task Case5()=>AdminReportUtcid.Run(125,5);
 [Fact(DisplayName="FUNC125-ADM-UTCID06")] public Task Case6()=>AdminReportUtcid.Run(125,6);
}
internal static class AdminReportUtcid
{
 internal static Task Run(int f,int n)=>f switch{120=>List(n,0),121=>Detail(n),122=>Resolve(n,false),123=>Resolve(n,true),124=>List(n,1),125=>List(n,2),_=>throw new ArgumentOutOfRangeException()};
 private static (AdminFeedbackReportsController C,Mock<IFeedbackReportService>S,Guid A) New(bool auth=true){var s=new Mock<IFeedbackReportService>();var a=Guid.NewGuid();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.ToString())]));return(new AdminFeedbackReportsController(s.Object){ControllerContext=new ControllerContext{HttpContext=ctx}},s,a);}
 private static async Task List(int n,int mode){var(c,s,_)=New();var q=new FeedbackReportQuery{Keyword=mode==1?(n==2?"":n==3?"spam":new string('x',n==5?1000:3)):null,FeedbackType=mode==2&&n==2?"System":null,Status=mode==2&&n==3?"Dismissed":"Pending",ReasonCode=mode==2&&n==4?"Abuse":null,Page=n==4?int.MaxValue:1,PageSize=n==4?int.MaxValue:20};if(n==6)s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new FeedbackReportValidationException("invalid"));else s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<FeedbackReportListItemDto>{Page=q.Page,PageSize=q.PageSize,TotalItems=n==2?0:1});var r=await c.GetList(q,default);Assert.Equal(n==6?400:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Detail(int n){var(c,s,_)=New();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==6){s.Setup(x=>x.GetDetailsAsync(id,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetDetails(id,default));return;}s.Setup(x=>x.GetDetailsAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(n==2?null:new FeedbackReportDetailsDto{ReportId=id,Status=n==5?"Dismissed":"Pending"});var r=await c.GetDetails(id,default);Assert.Equal(n==2?404:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Resolve(int n,bool delete){var(c,s,a)=New(n!=2);var id=n==5?Guid.Empty:Guid.NewGuid();var q=new ResolveFeedbackReportRequest{ResolutionNote="reviewed"};if(n==1){if(delete)s.Setup(x=>x.DeleteFeedbackAsync(id,a,q,It.IsAny<CancellationToken>())).ReturnsAsync(new FeedbackReportResolutionDto{ReportId=id,Status="Resolved",FeedbackDeleted=true});else s.Setup(x=>x.DismissAsync(id,a,q,It.IsAny<CancellationToken>())).ReturnsAsync(new FeedbackReportResolutionDto{ReportId=id,Status="Dismissed"});}else if(n!=2){Exception e=n==3?new FeedbackReportValidationException("invalid"):n==4?new FeedbackReportForbiddenException("forbidden"):n==5?new FeedbackReportNotFoundException():new FeedbackReportConflictException("resolved");if(delete)s.Setup(x=>x.DeleteFeedbackAsync(id,a,q,It.IsAny<CancellationToken>())).ThrowsAsync(e);else s.Setup(x=>x.DismissAsync(id,a,q,It.IsAny<CancellationToken>())).ThrowsAsync(e);}var r=delete?await c.DeleteFeedback(id,q,default):await c.Dismiss(id,q,default);Assert.Equal(n==1?200:n==2?401:n==3?400:n==4?403:n==5?404:409,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
