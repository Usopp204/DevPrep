using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserAPI.Controllers;
using UserAPI.DTOs;
using UserAPI.Models;
using Xunit;
using static UserAPI.Tests.Controllers.ManagerUserTestDb;

namespace UserAPI.Tests.Controllers;

public class ManagerUserListUtcidTests
{
 [Fact(DisplayName="FUNC161-MGR-UTCID01")] public async Task List_returns_core_result(){await using var db=Db();var expected=new OkObjectResult("users");var c=new TestController(db){ListResult=expected};Assert.Same(expected,await c.GetList());}
 [Fact(DisplayName="FUNC161-MGR-UTCID02")] public async Task List_forwards_valid_pagination(){await using var db=Db();var c=new TestController(db);_ =await c.GetList(2,25);Assert.Equal((2,25),(c.Page,c.PageSize));}
 [Fact(DisplayName="FUNC161-MGR-UTCID03")] public async Task List_supports_maximum_page_size(){await using var db=Db();var c=new TestController(db);_ =await c.GetList(int.MaxValue,100);Assert.Equal((int.MaxValue,100),(c.Page,c.PageSize));}
 [Fact(DisplayName="FUNC161-MGR-UTCID04")] public async Task List_normalizes_invalid_page(){await using var db=Db();var c=new TestController(db);_ =await c.GetList(0,10);Assert.Equal(1,c.Page);}
 [Fact(DisplayName="FUNC161-MGR-UTCID05")] public async Task List_normalizes_sizes_outside_range(){await using var db=Db();var c=new TestController(db);_ =await c.GetList(1,0);Assert.Equal(10,c.PageSize);_ =await c.GetList(1,101);Assert.Equal(10,c.PageSize);}
 [Fact(DisplayName="FUNC161-MGR-UTCID06")] public void List_authorizes_manager(){var roles=Assert.Single(typeof(ManagerUsersController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles!;Assert.Contains("Manager",roles.Split(','));}
}

public class ManagerUserDetailUtcidTests
{
 [Fact(DisplayName="FUNC162-MGR-UTCID01")] public async Task Detail_returns_core_success(){await using var db=Db();var id=Guid.NewGuid();var expected=new OkObjectResult(id);var c=new TestController(db){DetailResult=expected};Assert.Same(expected,await c.GetDetails(id));Assert.Equal(id,c.Id);}
 [Fact(DisplayName="FUNC162-MGR-UTCID02")] public async Task Detail_returns_core_not_found(){await using var db=Db();var expected=new NotFoundObjectResult("missing");var c=new TestController(db){DetailResult=expected};Assert.Same(expected,await c.GetDetails(Guid.NewGuid()));}
 [Fact(DisplayName="FUNC162-MGR-UTCID03")] public async Task Detail_can_return_complete_dto_envelope(){await using var db=Db();var dto=new ManagerUserDetailsDto{UserId=Guid.NewGuid(),Email="user@example.com",CreditBalance=12};var expected=new OkObjectResult(ApiResponse<ManagerUserDetailsDto>.Ok(dto));var c=new TestController(db){DetailResult=expected};var result=Assert.IsType<OkObjectResult>(await c.GetDetails(dto.UserId));Assert.Same(dto,Assert.IsType<ApiResponse<ManagerUserDetailsDto>>(result.Value).Data);}
 [Fact(DisplayName="FUNC162-MGR-UTCID04")] public async Task Detail_empty_guid_boundary_is_forwarded(){await using var db=Db();var c=new TestController(db);_ =await c.GetDetails(Guid.Empty);Assert.Equal(Guid.Empty,c.Id);}
 [Fact(DisplayName="FUNC162-MGR-UTCID05")] public async Task Detail_preserves_maximum_credit_boundary(){await using var db=Db();var dto=new ManagerUserDetailsDto{CreditBalance=int.MaxValue};var c=new TestController(db){DetailResult=new OkObjectResult(dto)};var result=Assert.IsType<OkObjectResult>(await c.GetDetails(Guid.NewGuid()));Assert.Equal(int.MaxValue,Assert.IsType<ManagerUserDetailsDto>(result.Value).CreditBalance);}
 [Fact(DisplayName="FUNC162-MGR-UTCID06")] public async Task Detail_propagates_query_failure(){await using var db=Db();var c=new TestController(db){Failure=new TimeoutException()};await Assert.ThrowsAsync<TimeoutException>(()=>c.GetDetails(Guid.NewGuid()));}
}

public class ManagerSearchUserUtcidTests
{
 [Fact(DisplayName="FUNC163-MGR-UTCID01")] public Task Search_by_name()=>Search("Nguyen Van A");
 [Fact(DisplayName="FUNC163-MGR-UTCID02")] public Task Search_by_email()=>Search("user@example.com");
 [Fact(DisplayName="FUNC163-MGR-UTCID03")] public Task Search_preserves_unicode()=>Search("Đặng");
 [Fact(DisplayName="FUNC163-MGR-UTCID04")] public Task Search_preserves_blank_for_core_handling()=>Search(" ");
 [Fact(DisplayName="FUNC163-MGR-UTCID05")] public Task Search_supports_long_boundary()=>Search(new string('x',200));
 [Fact(DisplayName="FUNC163-MGR-UTCID06")] public async Task Search_propagates_query_failure(){await using var db=Db();var c=new TestController(db){Failure=new InvalidOperationException()};await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetList(keyword:"fail"));}
 private static async Task Search(string keyword){await using var db=Db();var c=new TestController(db);_ =await c.GetList(keyword:keyword);Assert.Equal(keyword,c.Keyword);}
}

public class ManagerFilterUserUtcidTests
{
 [Fact(DisplayName="FUNC164-MGR-UTCID01")] public Task Filter_by_role()=>Filter("Candidate",null,null,null);
 [Fact(DisplayName="FUNC164-MGR-UTCID02")] public Task Filter_by_status()=>Filter(null,"Active",null,null);
 [Fact(DisplayName="FUNC164-MGR-UTCID03")] public Task Filter_by_created_from()=>Filter(null,null,new DateTime(2026,1,1),null);
 [Fact(DisplayName="FUNC164-MGR-UTCID04")] public Task Filter_by_created_to()=>Filter(null,null,null,new DateTime(2026,12,31));
 [Fact(DisplayName="FUNC164-MGR-UTCID05")] public Task Filter_preserves_date_boundaries()=>Filter(null,null,DateTime.MinValue,DateTime.MaxValue);
 [Fact(DisplayName="FUNC164-MGR-UTCID06")] public async Task Combined_filters_are_forwarded(){await using var db=Db();var c=new TestController(db);var from=new DateTime(2026,1,1);var to=new DateTime(2026,1,31);_ =await c.GetList(3,50,"mail","Candidate","Blocked",from,to);Assert.Equal((3,50,"mail","Candidate","Blocked",from,to),(c.Page,c.PageSize,c.Keyword,c.Role,c.Status,c.From,c.To));}
 private static async Task Filter(string? role,string? status,DateTime? from,DateTime? to){await using var db=Db();var c=new TestController(db);_ =await c.GetList(role:role,status:status,createdFrom:from,createdTo:to);Assert.Equal((role,status,from,to),(c.Role,c.Status,c.From,c.To));}
}

file sealed class TestController(UserDbContext db):ManagerUsersController(db)
{
 public int Page;public int PageSize;public string? Keyword;public string? Role;public string? Status;public DateTime? From;public DateTime? To;public Guid Id;
 public IActionResult ListResult=new OkObjectResult("ok");public IActionResult DetailResult=new OkObjectResult("ok");public Exception? Failure;
 protected override Task<IActionResult> GetListCoreAsync(int page,int pageSize,string? keyword,string? role,string? status,DateTime? createdFrom,DateTime? createdTo){if(Failure is not null)throw Failure;Page=page;PageSize=pageSize;Keyword=keyword;Role=role;Status=status;From=createdFrom;To=createdTo;return Task.FromResult(ListResult);}
 protected override Task<IActionResult> GetDetailsCoreAsync(Guid id){if(Failure is not null)throw Failure;Id=id;return Task.FromResult(DetailResult);}
}

file static class ManagerUserTestDb
{
 public static UserDbContext Db()=>new(new DbContextOptionsBuilder<UserDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
