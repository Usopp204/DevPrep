using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;
using Xunit;
namespace UserAPI.Tests.Controllers;
public class Func98UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC098-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(98,1);
 [Fact(DisplayName="FUNC098-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(98,2);
 [Fact(DisplayName="FUNC098-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(98,3);
 [Fact(DisplayName="FUNC098-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(98,4);
 [Fact(DisplayName="FUNC098-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(98,5);
 [Fact(DisplayName="FUNC098-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(98,6);
}
public class Func99UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC099-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(99,1);
 [Fact(DisplayName="FUNC099-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(99,2);
 [Fact(DisplayName="FUNC099-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(99,3);
 [Fact(DisplayName="FUNC099-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(99,4);
 [Fact(DisplayName="FUNC099-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(99,5);
 [Fact(DisplayName="FUNC099-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(99,6);
}
public class Func100UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC100-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(100,1);
 [Fact(DisplayName="FUNC100-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(100,2);
 [Fact(DisplayName="FUNC100-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(100,3);
 [Fact(DisplayName="FUNC100-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(100,4);
 [Fact(DisplayName="FUNC100-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(100,5);
 [Fact(DisplayName="FUNC100-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(100,6);
}
public class Func101UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC101-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(101,1);
 [Fact(DisplayName="FUNC101-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(101,2);
 [Fact(DisplayName="FUNC101-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(101,3);
 [Fact(DisplayName="FUNC101-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(101,4);
 [Fact(DisplayName="FUNC101-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(101,5);
 [Fact(DisplayName="FUNC101-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(101,6);
}
public class Func102UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC102-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(102,1);
 [Fact(DisplayName="FUNC102-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(102,2);
 [Fact(DisplayName="FUNC102-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(102,3);
 [Fact(DisplayName="FUNC102-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(102,4);
 [Fact(DisplayName="FUNC102-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(102,5);
 [Fact(DisplayName="FUNC102-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(102,6);
}
public class Func103UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC103-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(103,1);
 [Fact(DisplayName="FUNC103-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(103,2);
 [Fact(DisplayName="FUNC103-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(103,3);
 [Fact(DisplayName="FUNC103-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(103,4);
 [Fact(DisplayName="FUNC103-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(103,5);
 [Fact(DisplayName="FUNC103-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(103,6);
}
public class Func104UserAdminUtcidTests
{
 [Fact(DisplayName="FUNC104-ADM-UTCID01")] public Task Case1()=>AdminUserUtcid.Run(104,1);
 [Fact(DisplayName="FUNC104-ADM-UTCID02")] public Task Case2()=>AdminUserUtcid.Run(104,2);
 [Fact(DisplayName="FUNC104-ADM-UTCID03")] public Task Case3()=>AdminUserUtcid.Run(104,3);
 [Fact(DisplayName="FUNC104-ADM-UTCID04")] public Task Case4()=>AdminUserUtcid.Run(104,4);
 [Fact(DisplayName="FUNC104-ADM-UTCID05")] public Task Case5()=>AdminUserUtcid.Run(104,5);
 [Fact(DisplayName="FUNC104-ADM-UTCID06")] public Task Case6()=>AdminUserUtcid.Run(104,6);
}
internal static class AdminUserUtcid
{
 internal static Task Run(int f,int n)=>f switch{98=>List(n,0),99=>Detail(n),100=>Status(n,"Blocked"),101=>List(n,1),102=>List(n,2),103=>Status(n,"Active"),104=>Role(n),_=>throw new ArgumentOutOfRangeException()};
 private static (AdminUsersController C,Mock<IAdminUserService>S,Guid A) New(bool auth=true){var s=new Mock<IAdminUserService>();var a=Guid.NewGuid();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,a.ToString())]));return(new AdminUsersController(s.Object){ControllerContext=new ControllerContext{HttpContext=ctx}},s,a);}
 private static async Task List(int n,int mode){var(c,s,_)=New();var q=new AdminUserQueryDto{Keyword=mode==1?(n==2?"":n==3?"duy":new string('x',n==5?1000:3)):null,Role=mode==2&&n%2==0?"Manager":null,Status=mode==2&&n%2!=0?"Blocked":null,Page=n==4?int.MaxValue:1,PageSize=n==4?int.MaxValue:10};if(n==6){s.Setup(x=>x.GetUsersAsync(q)).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetUsers(q));return;}s.Setup(x=>x.GetUsersAsync(q)).ReturnsAsync(new PagedResult<AdminUserListItemDto>{PageIndex=q.Page,PageSize=q.PageSize,TotalCount=n==2?0:1});var body=Assert.IsType<ApiResponse<PagedResult<AdminUserListItemDto>>>(Assert.IsType<OkObjectResult>(await c.GetUsers(q)).Value);Assert.Equal(n==2?0:1,body.Data!.TotalCount);}
 private static async Task Detail(int n){var(c,s,_)=New();var id=n==3?Guid.Empty:n==4?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();if(n==6){s.Setup(x=>x.GetUserDetailAsync(id)).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetDetail(id));return;}s.Setup(x=>x.GetUserDetailAsync(id)).ReturnsAsync(n==2?null:new AdminUserDetailDto{UserId=id,Status=n==5?"Blocked":"Active"});var r=await c.GetDetail(id);Assert.Equal(n==2?404:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Status(int n,string status){var(c,s,a)=New(n!=6);var id=n==4?Guid.Empty:n==5?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();var q=new AdminUpdateUserStatusDto{Status=status,Reason="policy"};if(n==3)s.Setup(x=>x.UpdateStatusAsync(id,q,a)).ThrowsAsync(new InvalidOperationException("invalid"));else s.Setup(x=>x.UpdateStatusAsync(id,q,n==6?null:a)).ReturnsAsync(n!=2);var r=await c.UpdateStatus(id,q);Assert.Equal(n==2?404:n==3?400:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
 private static async Task Role(int n){var(c,s,a)=New(n!=6);var id=n==4?Guid.Empty:n==5?Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"):Guid.NewGuid();var q=new AdminUpdateUserRoleDto{Role=n==3?"Invalid":"Manager"};if(n==3)s.Setup(x=>x.UpdateRoleAsync(id,q,a)).ThrowsAsync(new InvalidOperationException("invalid role"));else s.Setup(x=>x.UpdateRoleAsync(id,q,n==6?null:a)).ReturnsAsync(n!=2);var r=await c.UpdateRole(id,q);Assert.Equal(n==2?404:n==3?400:200,Assert.IsAssignableFrom<ObjectResult>(r).StatusCode);}
}
