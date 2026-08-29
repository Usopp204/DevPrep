using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using Xunit;

namespace PaymentAPI.Tests;

public class CurrentCreditBalanceUtcidTests
{
 [Fact(DisplayName="FUNC077-USR-UTCID01")] public Task Positive_balance()=>Balance([100,50],150);
 [Fact(DisplayName="FUNC077-USR-UTCID02")] public Task Empty_ledger()=>Balance([],0);
 [Fact(DisplayName="FUNC077-USR-UTCID03")] public Task Usage_reduces_balance()=>Balance([100,-30],70);
 [Fact(DisplayName="FUNC077-USR-UTCID04")] public Task Refund_increases_balance()=>Balance([-20,20],0);
 [Fact(DisplayName="FUNC077-USR-UTCID05")] public async Task Other_user_is_excluded(){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();db.CreditLedgerEntries.AddRange(PayRepoTest.Ledger(user,10),PayRepoTest.Ledger(Guid.NewGuid(),999));await db.SaveChangesAsync();Assert.Equal(10,await new LedgerRepository(db).GetBalanceByUserAsync(user));}
 [Fact(DisplayName="FUNC077-USR-UTCID06")] public async Task Latest_change_timestamp_is_returned(){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();var latest=DateTime.UtcNow;db.CreditLedgerEntries.AddRange(PayRepoTest.Ledger(user,1,latest.AddDays(-1)),PayRepoTest.Ledger(user,1,latest));await db.SaveChangesAsync();Assert.Equal(latest,await new LedgerRepository(db).GetLastChangedAtByUserAsync(user));}
 private static async Task Balance(int[] changes,int expected){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();db.CreditLedgerEntries.AddRange(changes.Select(x=>PayRepoTest.Ledger(user,x)));await db.SaveChangesAsync();Assert.Equal(expected,await new LedgerRepository(db).GetBalanceByUserAsync(user));}
}

public class ViewCreditPackagesUtcidTests
{
 [Fact(DisplayName="FUNC078-USR-UTCID01")] public Task Active_package_is_returned()=>Packages(true,false,1);
 [Fact(DisplayName="FUNC078-USR-UTCID02")] public Task Empty_catalog_is_returned()=>Packages(false,false,0);
 [Fact(DisplayName="FUNC078-USR-UTCID03")] public Task Inactive_package_is_hidden()=>Packages(false,true,0);
 [Fact(DisplayName="FUNC078-USR-UTCID04")] public async Task Packages_are_sorted(){await using var db=PayRepoTest.Db();var second=PayRepoTest.Package(true,2);var first=PayRepoTest.Package(true,1);db.CreditPackages.AddRange(second,first);await db.SaveChangesAsync();Assert.Equal(new[]{first.PackageId,second.PackageId},(await new CreditPackageRepository(db).GetActiveAsync()).Select(x=>x.PackageId));}
 [Fact(DisplayName="FUNC078-USR-UTCID05")] public async Task Active_name_lookup_ignores_inactive(){await using var db=PayRepoTest.Db();var p=PayRepoTest.Package(false,1);p.Name="Starter";db.Add(p);await db.SaveChangesAsync();Assert.False(await new CreditPackageRepository(db).ExistsActiveNameAsync("Starter"));}
 [Fact(DisplayName="FUNC078-USR-UTCID06")] public async Task Package_value_boundaries_are_preserved(){await using var db=PayRepoTest.Db();var p=PayRepoTest.Package(true,0);p.Credits=int.MaxValue;p.Price=0;db.Add(p);await db.SaveChangesAsync();var result=Assert.Single(await new CreditPackageRepository(db).GetActiveAsync());Assert.Equal(int.MaxValue,result.Credits);Assert.Equal(0,result.Price);}
 private static async Task Packages(bool addActive,bool addInactive,int expected){await using var db=PayRepoTest.Db();if(addActive)db.Add(PayRepoTest.Package(true,1));if(addInactive)db.Add(PayRepoTest.Package(false,1));await db.SaveChangesAsync();Assert.Equal(expected,(await new CreditPackageRepository(db).GetActiveAsync()).Count());}
}

public class TransactionHistoryUtcidTests
{
 [Fact(DisplayName="FUNC082-USR-UTCID01")] public Task Paid_order_is_visible()=>History(["Paid"],1);
 [Fact(DisplayName="FUNC082-USR-UTCID02")] public Task Empty_history()=>History([],0);
 [Fact(DisplayName="FUNC082-USR-UTCID03")] public Task Pending_order_is_hidden()=>History(["Pending"],0);
 [Fact(DisplayName="FUNC082-USR-UTCID04")] public Task Failed_order_is_visible()=>History(["Failed"],1);
 [Fact(DisplayName="FUNC082-USR-UTCID05")] public async Task Other_users_orders_are_hidden(){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();db.Orders.AddRange(PayRepoTest.Order(user,"Paid"),PayRepoTest.Order(Guid.NewGuid(),"Paid"));await db.SaveChangesAsync();Assert.Single((await new OrderRepository(db).GetPagedByUserAsync(user,1,10)).Items);}
 [Fact(DisplayName="FUNC082-USR-UTCID06")] public async Task History_paginates_newest_first(){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();var old=PayRepoTest.Order(user,"Paid",DateTime.UtcNow.AddDays(-1));var latest=PayRepoTest.Order(user,"Paid",DateTime.UtcNow);db.AddRange(old,latest);await db.SaveChangesAsync();var page=await new OrderRepository(db).GetPagedByUserAsync(user,1,1);Assert.Equal(latest.OrderId,Assert.Single(page.Items).OrderId);Assert.Equal(2,page.TotalCount);}
 private static async Task History(string[] statuses,int expected){await using var db=PayRepoTest.Db();var user=Guid.NewGuid();db.Orders.AddRange(statuses.Select(x=>PayRepoTest.Order(user,x)));await db.SaveChangesAsync();Assert.Equal(expected,(await new OrderRepository(db).GetPagedByUserAsync(user,1,10)).Items.Count);}
}

internal static class PayRepoTest
{
 internal static PaymentDbContext Db()=>new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
 internal static CreditLedgerEntry Ledger(Guid user,int change,DateTime? time=null)=>new(){LedgerId=Guid.NewGuid(),UserId=user,CreditChange=change,EntryType=change<0?"Usage":"Purchase",CreatedAt=time??DateTime.UtcNow};
 internal static CreditPackage Package(bool active,int order)=>new(){PackageId=Guid.NewGuid(),Name=Guid.NewGuid().ToString(),Credits=10,Price=1000,IsActive=active,SortOrder=order,CreatedAt=DateTime.UtcNow,UpdatedAt=DateTime.UtcNow};
 internal static Order Order(Guid user,string status,DateTime? time=null)=>new(){OrderId=Guid.NewGuid(),OrderCode=Guid.NewGuid().ToString("N"),UserId=user,PackageId=Guid.NewGuid(),Amount=1000,Credits=10,Status=status,CreatedAt=time??DateTime.UtcNow,UpdatedAt=time??DateTime.UtcNow};
}
