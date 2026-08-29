using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentAPI.Controllers;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using Xunit;
using static PaymentAPI.Tests.ManagerPaymentSupport;

namespace PaymentAPI.Tests;

public class ManagerPaymentListUtcidTests
{
 [Fact(DisplayName="FUNC181-MGR-UTCID01")] public async Task List_returns_orders_and_summary(){await using var db=Db();db.AddRange(Order("Paid",100),Order("Pending",50));await db.SaveChangesAsync();var data=await List(db);Assert.Equal(2,data.TotalCount);Assert.Equal(150,data.TotalAmount);Assert.Equal(100,data.PaidAmount);Assert.Equal(1,data.PaidCount);}
 [Fact(DisplayName="FUNC181-MGR-UTCID02")] public async Task List_returns_latest_transaction(){await using var db=Db();var order=Order("Paid",100);db.Add(order);db.PaymentTransactions.AddRange(Tx(order,"old",DateTime.UtcNow.AddMinutes(-1)),Tx(order,"latest",DateTime.UtcNow));await db.SaveChangesAsync();Assert.Equal("latest",Assert.Single((await List(db)).Items).VnpTransactionNo);}
 [Fact(DisplayName="FUNC181-MGR-UTCID03")] public async Task List_returns_empty_summary(){await using var db=Db();var data=await List(db);Assert.Empty(data.Items);Assert.Equal(0,data.TotalCount);Assert.Equal(0,data.TotalAmount);}
 [Fact(DisplayName="FUNC181-MGR-UTCID04")] public async Task List_normalizes_invalid_page(){await using var db=Db();Assert.Equal(1,(await List(db,new(){Page=-1,PageSize=10})).Page);}
 [Fact(DisplayName="FUNC181-MGR-UTCID05")] public async Task List_clamps_page_size_boundaries(){await using var db=Db();Assert.Equal(1,(await List(db,new(){PageSize=0})).PageSize);Assert.Equal(100,(await List(db,new(){PageSize=int.MaxValue})).PageSize);}
 [Fact(DisplayName="FUNC181-MGR-UTCID06")] public void List_requires_manager_role(){var roles=Assert.Single(typeof(ManagerPaymentsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>()).Roles;Assert.Equal("Manager",roles);}
}

public class ManagerPaymentDetailUtcidTests
{
 [Fact(DisplayName="FUNC182-MGR-UTCID01")] public async Task Detail_maps_order_fields(){await using var db=Db();var order=Order("Paid",125);db.Add(order);await db.SaveChangesAsync();var data=await Detail(db,order.OrderId);Assert.Equal((order.OrderCode,125m,order.Credits),(data.OrderCode,data.Amount,data.Credits));}
 [Fact(DisplayName="FUNC182-MGR-UTCID02")] public async Task Detail_maps_latest_transaction(){await using var db=Db();var order=Order("Paid",100);var tx=Tx(order,"number",DateTime.UtcNow);tx.VnpBankCode="NCB";db.AddRange(order,tx);await db.SaveChangesAsync();var data=await Detail(db,order.OrderId);Assert.Equal("number",data.Transaction!.VnpTransactionNo);Assert.Equal("NCB",data.Transaction.VnpBankCode);}
 [Fact(DisplayName="FUNC182-MGR-UTCID03")] public async Task Detail_supports_order_without_transaction(){await using var db=Db();var order=Order("Pending",0);db.Add(order);await db.SaveChangesAsync();Assert.Null((await Detail(db,order.OrderId)).Transaction);}
 [Fact(DisplayName="FUNC182-MGR-UTCID04")] public async Task Detail_unknown_id_returns_not_found(){await using var db=Db();Assert.IsType<NotFoundObjectResult>(await Controller(db).GetById(Guid.NewGuid(),default));}
 [Fact(DisplayName="FUNC182-MGR-UTCID05")] public async Task Detail_empty_guid_boundary_returns_not_found(){await using var db=Db();Assert.IsType<NotFoundObjectResult>(await Controller(db).GetById(Guid.Empty,default));}
 [Fact(DisplayName="FUNC182-MGR-UTCID06")] public async Task Detail_honors_cancellation(){await using var db=Db();using var source=new CancellationTokenSource();source.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Controller(db).GetById(Guid.NewGuid(),source.Token));}
}

public class ManagerFilterPaymentUtcidTests
{
 [Fact(DisplayName="FUNC183-MGR-UTCID01")] public Task Filter_status()=>Filter(new(){Status="Paid"},o=>o.Status="Paid");
 [Fact(DisplayName="FUNC183-MGR-UTCID02")] public Task Filter_payment_method()=>Filter(new(){PaymentMethod="Cash"},o=>o.PaymentMethod="Cash");
 [Fact(DisplayName="FUNC183-MGR-UTCID03")] public async Task Filter_user_and_keyword(){await using var db=Db();var user=Guid.NewGuid();var match=Order("Paid",10,user,"TARGET-001");db.AddRange(match,Order("Paid",10));await db.SaveChangesAsync();Assert.Equal(match.OrderId,Assert.Single((await List(db,new(){UserId=user,Keyword=" TARGET "})).Items).OrderId);}
 [Fact(DisplayName="FUNC183-MGR-UTCID04")] public async Task Filter_date_range_is_inclusive_of_end_day(){await using var db=Db();var day=new DateTime(2026,1,1);db.AddRange(Order("Paid",10,time:day),Order("Paid",10,time:day.AddDays(1).AddTicks(-1)),Order("Paid",10,time:day.AddDays(1)));await db.SaveChangesAsync();Assert.Equal(2,(await List(db,new(){DateFrom=day,DateTo=day})).TotalCount);}
 [Fact(DisplayName="FUNC183-MGR-UTCID05")] public async Task Filter_amount_boundaries_are_inclusive(){await using var db=Db();db.AddRange(Order("Paid",10),Order("Paid",20),Order("Paid",21));await db.SaveChangesAsync();Assert.Equal(2,(await List(db,new(){MinAmount=10,MaxAmount=20})).TotalCount);}
 [Fact(DisplayName="FUNC183-MGR-UTCID06")] public async Task Combined_filters_return_empty_when_no_match(){await using var db=Db();db.Add(Order("Paid",100));await db.SaveChangesAsync();Assert.Empty((await List(db,new(){Status="Failed",PaymentMethod="Cash",MinAmount=1000})).Items);}
 private static async Task Filter(ManagerPaymentQuery query,Action<Order> change){await using var db=Db();var match=Order("Pending",10);change(match);db.AddRange(match,Order("Pending",10));await db.SaveChangesAsync();Assert.Equal(match.OrderId,Assert.Single((await List(db,query)).Items).OrderId);}
}

file static class ManagerPaymentSupport
{
 public static PaymentDbContext Db()=>new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 public static TestManagerPaymentsController Controller(PaymentDbContext db)=>new(db);
 public static Order Order(string status,decimal amount,Guid? user=null,string? code=null,DateTime? time=null){var now=time??DateTime.UtcNow;return new(){OrderId=Guid.NewGuid(),OrderCode=code??Guid.NewGuid().ToString("N"),UserId=user??Guid.NewGuid(),PackageId=Guid.NewGuid(),Amount=amount,Credits=10,Currency="VND",PaymentMethod="VNPay",Status=status,CreatedAt=now,UpdatedAt=now,PaidAt=status=="Paid"?now:null};}
 public static PaymentTransaction Tx(Order order,string number,DateTime time)=>new(){TransactionId=Guid.NewGuid(),OrderId=order.OrderId,Amount=order.Amount,VnpTransactionNo=number,Status="Success",CreatedAt=time};
 public static async Task<ManagerPaymentListResultDto> List(PaymentDbContext db,ManagerPaymentQuery? query=null)=>Assert.IsType<ApiResponse<ManagerPaymentListResultDto>>(Assert.IsType<OkObjectResult>(await Controller(db).GetList(query??new(),default)).Value).Data!;
 public static async Task<ManagerPaymentDetailDto> Detail(PaymentDbContext db,Guid id)=>Assert.IsType<ApiResponse<ManagerPaymentDetailDto>>(Assert.IsType<OkObjectResult>(await Controller(db).GetById(id,default)).Value).Data!;
}

file sealed class TestManagerPaymentsController(PaymentDbContext db):ManagerPaymentsController(db,NullLogger<ManagerPaymentsController>.Instance,new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"PAYMENT_DB_CONNECTION_STRING","Server=test"}}).Build())
{
 protected override Task EnrichWithUserNamesAsync(List<ManagerPaymentListItemDto> items,CancellationToken ct)=>Task.CompletedTask;
}
