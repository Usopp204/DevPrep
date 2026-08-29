using Microsoft.AspNetCore.Mvc;
using Moq;
using QuestionAPI.Controllers;
using QuestionAPI.DTOs;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Services.Interfaces;
using Xunit;
namespace QuestionAPI.Tests.Controllers;
public class Func92TechStackUtcidTests
{
 [Fact(DisplayName="FUNC092-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(92,1);
 [Fact(DisplayName="FUNC092-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(92,2);
 [Fact(DisplayName="FUNC092-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(92,3);
 [Fact(DisplayName="FUNC092-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(92,4);
 [Fact(DisplayName="FUNC092-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(92,5);
 [Fact(DisplayName="FUNC092-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(92,6);
}
public class Func93TechStackUtcidTests
{
 [Fact(DisplayName="FUNC093-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(93,1);
 [Fact(DisplayName="FUNC093-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(93,2);
 [Fact(DisplayName="FUNC093-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(93,3);
 [Fact(DisplayName="FUNC093-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(93,4);
 [Fact(DisplayName="FUNC093-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(93,5);
 [Fact(DisplayName="FUNC093-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(93,6);
}
public class Func94TechStackUtcidTests
{
 [Fact(DisplayName="FUNC094-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(94,1);
 [Fact(DisplayName="FUNC094-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(94,2);
 [Fact(DisplayName="FUNC094-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(94,3);
 [Fact(DisplayName="FUNC094-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(94,4);
 [Fact(DisplayName="FUNC094-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(94,5);
 [Fact(DisplayName="FUNC094-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(94,6);
}
public class Func95TechStackUtcidTests
{
 [Fact(DisplayName="FUNC095-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(95,1);
 [Fact(DisplayName="FUNC095-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(95,2);
 [Fact(DisplayName="FUNC095-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(95,3);
 [Fact(DisplayName="FUNC095-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(95,4);
 [Fact(DisplayName="FUNC095-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(95,5);
 [Fact(DisplayName="FUNC095-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(95,6);
}
public class Func96TechStackUtcidTests
{
 [Fact(DisplayName="FUNC096-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(96,1);
 [Fact(DisplayName="FUNC096-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(96,2);
 [Fact(DisplayName="FUNC096-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(96,3);
 [Fact(DisplayName="FUNC096-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(96,4);
 [Fact(DisplayName="FUNC096-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(96,5);
 [Fact(DisplayName="FUNC096-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(96,6);
}
public class Func97TechStackUtcidTests
{
 [Fact(DisplayName="FUNC097-ADM-UTCID01")] public Task Case1()=>TechStackUtcid.Run(97,1);
 [Fact(DisplayName="FUNC097-ADM-UTCID02")] public Task Case2()=>TechStackUtcid.Run(97,2);
 [Fact(DisplayName="FUNC097-ADM-UTCID03")] public Task Case3()=>TechStackUtcid.Run(97,3);
 [Fact(DisplayName="FUNC097-ADM-UTCID04")] public Task Case4()=>TechStackUtcid.Run(97,4);
 [Fact(DisplayName="FUNC097-ADM-UTCID05")] public Task Case5()=>TechStackUtcid.Run(97,5);
 [Fact(DisplayName="FUNC097-ADM-UTCID06")] public Task Case6()=>TechStackUtcid.Run(97,6);
}
internal static class TechStackUtcid
{
 internal static Task Run(int f,int n)=>f switch{92=>List(n,0),93=>Create(n),94=>Update(n),95=>Delete(n),96=>List(n,1),97=>List(n,2),_=>throw new ArgumentOutOfRangeException()};
 private static async Task List(int n,int mode){var s=new Mock<ITechStackService>();var q=new TechStackQueryDto{Search=mode==1?(n==2?"":n==3?".NET":new string('x',n==5?500:3)):null,Status=mode==2?(n%2==0?TechStackStatusFilter.Active:TechStackStatusFilter.Inactive):TechStackStatusFilter.All,PageIndex=n==4?int.MaxValue:1,PageSize=n==4?100:6};if(n==6)s.Setup(x=>x.GetPageAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackValidationException("invalid"));else s.Setup(x=>x.GetPageAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new TechStackPagedResultDto{PageIndex=q.PageIndex,PageSize=q.PageSize,TotalRecords=n==2?0:1});var r=await new AdminTechStacksController(s.Object).Get(q,default);Assert.Equal(n==6?400:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Create(int n){var s=new Mock<ITechStackService>();var q=new CreateTechStackDto{Name=n==5?new string('x',200):".NET",Description="Runtime"};if(n is 2 or 3)s.Setup(x=>x.CreateAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackValidationException("invalid"));else if(n==4)s.Setup(x=>x.CreateAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackDuplicateException("duplicate"));else if(n==6)s.Setup(x=>x.CreateAsync(q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackValidationException("dependency"));else s.Setup(x=>x.CreateAsync(q,It.IsAny<CancellationToken>())).ReturnsAsync(new TechStackDto{TechStackId=Guid.NewGuid(),Name=q.Name});var r=await new AdminTechStacksController(s.Object).Create(q,default);Assert.Equal(n==4?409:n is 2 or 3 or 6?400:201,r is CreatedAtActionResult?201:Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Update(int n){var s=new Mock<ITechStackService>();var id=n==4?Guid.Empty:n==5?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();var q=new UpdateTechStackDto{Name=".NET",IsActive=true};if(n==2)s.Setup(x=>x.UpdateAsync(id,q,It.IsAny<CancellationToken>())).ReturnsAsync((TechStackDto?)null);else if(n==3)s.Setup(x=>x.UpdateAsync(id,q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackValidationException("invalid"));else if(n==6)s.Setup(x=>x.UpdateAsync(id,q,It.IsAny<CancellationToken>())).ThrowsAsync(new TechStackDuplicateException("duplicate"));else s.Setup(x=>x.UpdateAsync(id,q,It.IsAny<CancellationToken>())).ReturnsAsync(new TechStackDto{TechStackId=id,Name=q.Name});var r=await new AdminTechStacksController(s.Object).Update(id,q,default);Assert.Equal(n==2?404:n==3?400:n==6?409:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Delete(int n){var s=new Mock<ITechStackService>();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==6)s.Setup(x=>x.DeactivateAsync(id,It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db"));else s.Setup(x=>x.DeactivateAsync(id,It.IsAny<CancellationToken>())).ReturnsAsync(n!=2&&n!=5);var ctl=new AdminTechStacksController(s.Object);if(n==6){await Assert.ThrowsAsync<InvalidOperationException>(()=>ctl.Deactivate(id,default));return;}var r=await ctl.Deactivate(id,default);Assert.Equal(n is 2 or 5?404:204,r is NoContentResult?204:Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
