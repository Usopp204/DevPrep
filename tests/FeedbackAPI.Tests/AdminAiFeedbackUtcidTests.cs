using FeedbackAPI.Controllers;
using FeedbackAPI.DTOs;
using FeedbackAPI.Services;
using FeedbackAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
namespace FeedbackAPI.Tests;
public class Func107AiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC107-ADM-UTCID01")] public Task Case1()=>AdminAiUtcid.Run(107,1);
 [Fact(DisplayName="FUNC107-ADM-UTCID02")] public Task Case2()=>AdminAiUtcid.Run(107,2);
 [Fact(DisplayName="FUNC107-ADM-UTCID03")] public Task Case3()=>AdminAiUtcid.Run(107,3);
 [Fact(DisplayName="FUNC107-ADM-UTCID04")] public Task Case4()=>AdminAiUtcid.Run(107,4);
 [Fact(DisplayName="FUNC107-ADM-UTCID05")] public Task Case5()=>AdminAiUtcid.Run(107,5);
 [Fact(DisplayName="FUNC107-ADM-UTCID06")] public Task Case6()=>AdminAiUtcid.Run(107,6);
}
public class Func108AiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC108-ADM-UTCID01")] public Task Case1()=>AdminAiUtcid.Run(108,1);
 [Fact(DisplayName="FUNC108-ADM-UTCID02")] public Task Case2()=>AdminAiUtcid.Run(108,2);
 [Fact(DisplayName="FUNC108-ADM-UTCID03")] public Task Case3()=>AdminAiUtcid.Run(108,3);
 [Fact(DisplayName="FUNC108-ADM-UTCID04")] public Task Case4()=>AdminAiUtcid.Run(108,4);
 [Fact(DisplayName="FUNC108-ADM-UTCID05")] public Task Case5()=>AdminAiUtcid.Run(108,5);
 [Fact(DisplayName="FUNC108-ADM-UTCID06")] public Task Case6()=>AdminAiUtcid.Run(108,6);
}
public class Func109AiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC109-ADM-UTCID01")] public Task Case1()=>AdminAiUtcid.Run(109,1);
 [Fact(DisplayName="FUNC109-ADM-UTCID02")] public Task Case2()=>AdminAiUtcid.Run(109,2);
 [Fact(DisplayName="FUNC109-ADM-UTCID03")] public Task Case3()=>AdminAiUtcid.Run(109,3);
 [Fact(DisplayName="FUNC109-ADM-UTCID04")] public Task Case4()=>AdminAiUtcid.Run(109,4);
 [Fact(DisplayName="FUNC109-ADM-UTCID05")] public Task Case5()=>AdminAiUtcid.Run(109,5);
 [Fact(DisplayName="FUNC109-ADM-UTCID06")] public Task Case6()=>AdminAiUtcid.Run(109,6);
}
public class Func110AiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC110-ADM-UTCID01")] public Task Case1()=>AdminAiUtcid.Run(110,1);
 [Fact(DisplayName="FUNC110-ADM-UTCID02")] public Task Case2()=>AdminAiUtcid.Run(110,2);
 [Fact(DisplayName="FUNC110-ADM-UTCID03")] public Task Case3()=>AdminAiUtcid.Run(110,3);
 [Fact(DisplayName="FUNC110-ADM-UTCID04")] public Task Case4()=>AdminAiUtcid.Run(110,4);
 [Fact(DisplayName="FUNC110-ADM-UTCID05")] public Task Case5()=>AdminAiUtcid.Run(110,5);
 [Fact(DisplayName="FUNC110-ADM-UTCID06")] public Task Case6()=>AdminAiUtcid.Run(110,6);
}
public class Func111AiFeedbackUtcidTests
{
 [Fact(DisplayName="FUNC111-ADM-UTCID01")] public Task Case1()=>AdminAiUtcid.Run(111,1);
 [Fact(DisplayName="FUNC111-ADM-UTCID02")] public Task Case2()=>AdminAiUtcid.Run(111,2);
 [Fact(DisplayName="FUNC111-ADM-UTCID03")] public Task Case3()=>AdminAiUtcid.Run(111,3);
 [Fact(DisplayName="FUNC111-ADM-UTCID04")] public Task Case4()=>AdminAiUtcid.Run(111,4);
 [Fact(DisplayName="FUNC111-ADM-UTCID05")] public Task Case5()=>AdminAiUtcid.Run(111,5);
 [Fact(DisplayName="FUNC111-ADM-UTCID06")] public Task Case6()=>AdminAiUtcid.Run(111,6);
}
internal static class AdminAiUtcid
{
 internal static Task Run(int f,int n)=>f switch{107=>List(n,0),108=>List(n,1),109=>List(n,2),110=>Delete(n),111=>Restore(n),_=>throw new ArgumentOutOfRangeException()};
 private static async Task List(int n,int mode){var s=new Mock<IAiFeedbackService>();var q=new AiFeedbackQuery{Keyword=mode==1?(n==2?"":n==3?"helpful":new string('x',n==5?1000:3)):null,Rating=mode==2&&n==2?(byte)1:mode==2&&n==3?(byte)5:null,IsHelpful=mode==2&&n==4?true:null,OnlyDeleted=mode==2&&n==5,Page=n==4?int.MaxValue:1,PageSize=n==4?int.MaxValue:20};if(n==6){s.Setup(x=>x.GetListAsync(q)).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>new AdminAiFeedbackController(s.Object).GetList(q));return;}s.Setup(x=>x.GetListAsync(q)).ReturnsAsync(new PagedResult<AiFeedbackListItemDto>{Page=q.Page,PageSize=q.PageSize,TotalItems=n==2?0:1});var body=Assert.IsType<ApiResponse<PagedResult<AiFeedbackListItemDto>>>(Assert.IsType<OkObjectResult>(await new AdminAiFeedbackController(s.Object).GetList(q)).Value);Assert.Equal(n==2?0:1,body.Data!.TotalItems);}
 private static async Task Delete(int n){var s=new Mock<IAiFeedbackService>();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==6)s.Setup(x=>x.SoftDeleteAsync(id)).ThrowsAsync(new InvalidOperationException("db"));else s.Setup(x=>x.SoftDeleteAsync(id)).ReturnsAsync(n is not (2 or 5));var c=new AdminAiFeedbackController(s.Object);if(n==6){await Assert.ThrowsAsync<InvalidOperationException>(()=>c.Delete(id));return;}var r=await c.Delete(id);Assert.Equal(n is 2 or 5?404:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Restore(int n){var s=new Mock<IAiFeedbackService>();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==5)s.Setup(x=>x.RestoreAsync(id)).ThrowsAsync(new FeedbackEligibilityException("reported"));else if(n==6)s.Setup(x=>x.RestoreAsync(id)).ThrowsAsync(new InvalidOperationException("db"));else s.Setup(x=>x.RestoreAsync(id)).ReturnsAsync(n!=2);var c=new AdminAiFeedbackController(s.Object);if(n==6){await Assert.ThrowsAsync<InvalidOperationException>(()=>c.Restore(id));return;}var r=await c.Restore(id);Assert.Equal(n==2?404:n==5?409:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
