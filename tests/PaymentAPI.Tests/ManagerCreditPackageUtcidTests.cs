using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaymentAPI.Controllers;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class ManagerCreditPackageUtcidTests
{
 [Fact(DisplayName="FUNC189-MGR-UTCID01")] public async Task List_returns_items(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ReturnsAsync(Page("Basic"));Assert.Equal("Basic",Data(await c.GetList()).Items.Single().Name);}
 [Fact(DisplayName="FUNC189-MGR-UTCID02")] public async Task List_returns_activity_counts(){await using var d=Db();d.CreditPackages.AddRange(Pkg(true),Pkg(false));await d.SaveChangesAsync();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ReturnsAsync(Page());var r=Data(await c.GetList());Assert.Equal((1,1),(r.ActiveCount,r.InactiveCount));}
 [Fact(DisplayName="FUNC189-MGR-UTCID03")] public async Task List_empty_result(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ReturnsAsync(Page());Assert.Empty(Data(await c.GetList()).Items);}
 [Fact(DisplayName="FUNC189-MGR-UTCID04")] public async Task List_normalizes_bad_page(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ReturnsAsync(Page());await c.GetList(-1);s.Verify(x=>x.GetListAsync(1,10,null,null));}
 [Fact(DisplayName="FUNC189-MGR-UTCID05")] public async Task List_normalizes_page_size_boundaries(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ReturnsAsync(Page());await c.GetList(1,101);s.Verify(x=>x.GetListAsync(1,10,null,null));}
 [Fact(DisplayName="FUNC189-MGR-UTCID06")] public async Task List_propagates_failure(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,null,null)).ThrowsAsync(new TimeoutException());await Assert.ThrowsAsync<TimeoutException>(()=>c.GetList());}

 [Fact(DisplayName="FUNC190-MGR-UTCID01")] public async Task Detail_returns_item(){await using var d=Db();var(c,s)=New(d);var id=Guid.NewGuid();s.Setup(x=>x.GetByIdAsync(id)).ReturnsAsync(Dto(id));Assert.Equal(id,Detail(await c.GetById(id)).PackageId);}
 [Fact(DisplayName="FUNC190-MGR-UTCID02")] public async Task Detail_returns_inactive(){await using var d=Db();var(c,s)=New(d);var id=Guid.NewGuid();var dto=Dto(id);dto.IsActive=false;s.Setup(x=>x.GetByIdAsync(id)).ReturnsAsync(dto);Assert.False(Detail(await c.GetById(id)).IsActive);}
 [Fact(DisplayName="FUNC190-MGR-UTCID03")] public async Task Detail_missing_is_404(){await using var d=Db();var(c,_)=New(d);Assert.IsType<NotFoundObjectResult>(await c.GetById(Guid.NewGuid()));}
 [Fact(DisplayName="FUNC190-MGR-UTCID04")] public async Task Detail_empty_id_is_forwarded(){await using var d=Db();var(c,s)=New(d);Assert.IsType<NotFoundObjectResult>(await c.GetById(Guid.Empty));s.Verify(x=>x.GetByIdAsync(Guid.Empty));}
 [Fact(DisplayName="FUNC190-MGR-UTCID05")] public async Task Detail_preserves_numeric_boundaries(){await using var d=Db();var(c,s)=New(d);var id=Guid.NewGuid();var dto=Dto(id);dto.Credits=int.MaxValue;dto.Price=decimal.MaxValue;s.Setup(x=>x.GetByIdAsync(id)).ReturnsAsync(dto);var r=Detail(await c.GetById(id));Assert.Equal((int.MaxValue,decimal.MaxValue),(r.Credits,r.Price));}
 [Fact(DisplayName="FUNC190-MGR-UTCID06")] public async Task Detail_propagates_failure(){await using var d=Db();var(c,s)=New(d);var id=Guid.NewGuid();s.Setup(x=>x.GetByIdAsync(id)).ThrowsAsync(new InvalidOperationException());await Assert.ThrowsAsync<InvalidOperationException>(()=>c.GetById(id));}

 [Fact(DisplayName="FUNC191-MGR-UTCID01")] public async Task Search_forwards_keyword(){await Search("pro","Pro");}
 [Fact(DisplayName="FUNC191-MGR-UTCID02")] public async Task Search_preserves_whitespace(){await Search("  pro  ","Pro");}
 [Fact(DisplayName="FUNC191-MGR-UTCID03")] public async Task Search_no_match_empty(){await Search("missing",null);}
 [Fact(DisplayName="FUNC191-MGR-UTCID04")] public async Task Search_null_lists_all(){await Search(null,"Basic");}
 [Fact(DisplayName="FUNC191-MGR-UTCID05")] public async Task Search_empty_is_forwarded(){await Search("",null);}
 [Fact(DisplayName="FUNC191-MGR-UTCID06")] public async Task Search_long_term_is_forwarded(){await Search(new string('x',1000),null);}

 [Fact(DisplayName="FUNC192-MGR-UTCID01")] public async Task Filter_active(){await Filter(true,"Active");}
 [Fact(DisplayName="FUNC192-MGR-UTCID02")] public async Task Filter_inactive(){await Filter(false,"Inactive");}
 [Fact(DisplayName="FUNC192-MGR-UTCID03")] public async Task Filter_all(){await Filter(null,"Any");}
 [Fact(DisplayName="FUNC192-MGR-UTCID04")] public async Task Filter_active_empty(){await Filter(true,null);}
 [Fact(DisplayName="FUNC192-MGR-UTCID05")] public async Task Filter_combines_keyword(){await Filter(false,"Legacy","old");}
 [Fact(DisplayName="FUNC192-MGR-UTCID06")] public async Task Filter_preserves_page_boundary(){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(int.MaxValue,100,null,true)).ReturnsAsync(Page());await c.GetList(int.MaxValue,100,null,true);s.VerifyAll();}

 static async Task Search(string? keyword,string? result){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,keyword,null)).ReturnsAsync(Page(result));var r=Data(await c.GetList(keyword:keyword));s.Verify(x=>x.GetListAsync(1,10,keyword,null));Assert.Equal(result is null?0:1,r.Items.Count);}
 static async Task Filter(bool? active,string? result,string? keyword=null){await using var d=Db();var(c,s)=New(d);s.Setup(x=>x.GetListAsync(1,10,keyword,active)).ReturnsAsync(Page(result));var r=Data(await c.GetList(keyword:keyword,isActive:active));s.Verify(x=>x.GetListAsync(1,10,keyword,active));Assert.Equal(result is null?0:1,r.Items.Count);}
 static (ManagerCreditPackagesController,Mock<ICreditPackageService>) New(PaymentDbContext d){var s=new Mock<ICreditPackageService>();return(new(s.Object,d),s);}
 static PagedResult<CreditPackageDto> Data(IActionResult r)=>Assert.IsType<ApiResponse<PagedResult<CreditPackageDto>>>(Assert.IsType<OkObjectResult>(r).Value).Data!;
 static CreditPackageDto Detail(IActionResult r)=>Assert.IsType<ApiResponse<CreditPackageDto>>(Assert.IsType<OkObjectResult>(r).Value).Data!;
 static PagedResult<CreditPackageDto> Page(string? n=null)=>new(){Items=n is null?[]:[new CreditPackageDto{Name=n}]};
 static CreditPackageDto Dto(Guid id)=>new(){PackageId=id,Name="Pro",Credits=10,Price=100};
 static CreditPackage Pkg(bool active)=>new(){PackageId=Guid.NewGuid(),Name=Guid.NewGuid().ToString(),IsActive=active};
 static PaymentDbContext Db()=>new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
