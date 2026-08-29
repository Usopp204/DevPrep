using InterviewAPI.Controllers;
using InterviewAPI.DTOs;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class Func134AdminInterviewUtcidTests
{
 [Fact(DisplayName="FUNC134-ADM-UTCID01")] public Task Case1()=>AdminInterviewUtcid.List(1,0);
 [Fact(DisplayName="FUNC134-ADM-UTCID02")] public Task Case2()=>AdminInterviewUtcid.List(2,0);
 [Fact(DisplayName="FUNC134-ADM-UTCID03")] public Task Case3()=>AdminInterviewUtcid.List(3,0);
 [Fact(DisplayName="FUNC134-ADM-UTCID04")] public Task Case4()=>AdminInterviewUtcid.List(4,0);
 [Fact(DisplayName="FUNC134-ADM-UTCID05")] public Task Case5()=>AdminInterviewUtcid.List(5,0);
 [Fact(DisplayName="FUNC134-ADM-UTCID06")] public Task Case6()=>AdminInterviewUtcid.List(6,0);
}
public class Func135AdminInterviewUtcidTests
{
 [Fact(DisplayName="FUNC135-ADM-UTCID01")] public Task Case1()=>AdminInterviewUtcid.Detail(1);
 [Fact(DisplayName="FUNC135-ADM-UTCID02")] public Task Case2()=>AdminInterviewUtcid.Detail(2);
 [Fact(DisplayName="FUNC135-ADM-UTCID03")] public Task Case3()=>AdminInterviewUtcid.Detail(3);
 [Fact(DisplayName="FUNC135-ADM-UTCID04")] public Task Case4()=>AdminInterviewUtcid.Detail(4);
 [Fact(DisplayName="FUNC135-ADM-UTCID05")] public Task Case5()=>AdminInterviewUtcid.Detail(5);
 [Fact(DisplayName="FUNC135-ADM-UTCID06")] public Task Case6()=>AdminInterviewUtcid.Detail(6);
}
public class Func136AdminInterviewUtcidTests
{
 [Fact(DisplayName="FUNC136-ADM-UTCID01")] public Task Case1()=>AdminInterviewUtcid.List(1,1);
 [Fact(DisplayName="FUNC136-ADM-UTCID02")] public Task Case2()=>AdminInterviewUtcid.List(2,1);
 [Fact(DisplayName="FUNC136-ADM-UTCID03")] public Task Case3()=>AdminInterviewUtcid.List(3,1);
 [Fact(DisplayName="FUNC136-ADM-UTCID04")] public Task Case4()=>AdminInterviewUtcid.List(4,1);
 [Fact(DisplayName="FUNC136-ADM-UTCID05")] public Task Case5()=>AdminInterviewUtcid.List(5,1);
 [Fact(DisplayName="FUNC136-ADM-UTCID06")] public Task Case6()=>AdminInterviewUtcid.List(6,1);
}
public class Func137AdminInterviewUtcidTests
{
 [Fact(DisplayName="FUNC137-ADM-UTCID01")] public Task Case1()=>AdminInterviewUtcid.List(1,2);
 [Fact(DisplayName="FUNC137-ADM-UTCID02")] public Task Case2()=>AdminInterviewUtcid.List(2,2);
 [Fact(DisplayName="FUNC137-ADM-UTCID03")] public Task Case3()=>AdminInterviewUtcid.List(3,2);
 [Fact(DisplayName="FUNC137-ADM-UTCID04")] public Task Case4()=>AdminInterviewUtcid.List(4,2);
 [Fact(DisplayName="FUNC137-ADM-UTCID05")] public Task Case5()=>AdminInterviewUtcid.List(5,2);
 [Fact(DisplayName="FUNC137-ADM-UTCID06")] public Task Case6()=>AdminInterviewUtcid.List(6,2);
}

internal static class AdminInterviewUtcid
{
 internal static async Task List(int n,int mode){var s=new Mock<IInterviewService>();var q=new InterviewQuery{Keyword=mode==1?(n==2?"":n==3?"candidate":new string('x',n==5?1000:3)):null,Status=mode==2&&n==2?"Completed":null,Level=mode==2&&n==3?"Senior":null,InteractionMode=mode==2&&n==4?"Voice":null,OnlyDeleted=mode==2&&n==5,Page=n==4?int.MaxValue:1,PageSize=n==4?int.MaxValue:20};if(n==6){s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>new AdminInterviewController(s.Object).GetList(q,default));return;}s.Setup(x=>x.GetListAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new PagedResult<InterviewListItemDto>{Page=q.Page,PageSize=q.PageSize,TotalItems=n==2?0:1});var body=Assert.IsType<ApiResponse<PagedResult<InterviewListItemDto>>>(Assert.IsType<OkObjectResult>(await new AdminInterviewController(s.Object).GetList(q,default)).Value);Assert.Equal(n==2?0:1,body.Data!.TotalItems);}
 internal static async Task Detail(int n){var s=new Mock<IInterviewService>();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==6){s.Setup(x=>x.GetDetailAsync(id,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>new AdminInterviewController(s.Object).GetDetail(id,default));return;}s.Setup(x=>x.GetDetailAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(n==2?null:new InterviewDetailDto{SessionId=id,Status=n==5?"Cancelled":"Completed"});var r=await new AdminInterviewController(s.Object).GetDetail(id,default);Assert.Equal(n==2?404:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
