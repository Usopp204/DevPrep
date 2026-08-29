using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Endpoints;
using PaymentAPI.Models;
using Xunit;

namespace PaymentAPI.Tests;

public class ViewCreditHistoryUtcidTests
{
    [Fact(DisplayName="FUNC017-USR-UTCID01")] public async Task User_sees_own_history() { await using var db = CreditHistoryTest.Db(); var user = Guid.NewGuid(); db.CreditLedgerEntries.Add(CreditHistoryTest.Entry(user, "Purchase", 100)); await db.SaveChangesAsync(); var value = await CreditHistoryTest.Get(db, user); Assert.Equal(1, CreditHistoryTest.Int(value, "TotalCount")); }
    [Fact(DisplayName="FUNC017-USR-UTCID02")] public async Task Empty_history_is_returned() { await using var db = CreditHistoryTest.Db(); var value = await CreditHistoryTest.Get(db, Guid.NewGuid()); Assert.Equal(0, CreditHistoryTest.Int(value, "TotalCount")); }
    [Fact(DisplayName="FUNC017-USR-UTCID03")] public async Task Other_users_entries_are_hidden() { await using var db = CreditHistoryTest.Db(); db.CreditLedgerEntries.Add(CreditHistoryTest.Entry(Guid.NewGuid(), "Purchase", 100)); await db.SaveChangesAsync(); Assert.Equal(0, CreditHistoryTest.Int(await CreditHistoryTest.Get(db, Guid.NewGuid()), "TotalCount")); }
    [Fact(DisplayName="FUNC017-USR-UTCID04")] public async Task Invalid_header_is_bad_request() { await using var db = CreditHistoryTest.Db(); var ctx = new DefaultHttpContext(); ctx.Request.Headers["X-User-Id"] = "invalid"; Assert.Equal(400, (await CreditLedgerEndpoints.HandleAsync(ctx, db) as IStatusCodeHttpResult)!.StatusCode); }
    [Fact(DisplayName="FUNC017-USR-UTCID05")] public async Task Entries_are_newest_first() { await using var db = CreditHistoryTest.Db(); var user = Guid.NewGuid(); var old = CreditHistoryTest.Entry(user, "Purchase", 1, DateTime.UtcNow.AddDays(-1)); var recent = CreditHistoryTest.Entry(user, "Refund", 2, DateTime.UtcNow); db.AddRange(old, recent); await db.SaveChangesAsync(); var items = CreditHistoryTest.Items(await CreditHistoryTest.Get(db, user)); Assert.Equal(recent.LedgerId, CreditHistoryTest.GuidValue(items[0], "LedgerId")); }
    [Fact(DisplayName="FUNC017-USR-UTCID06")] public async Task Usage_and_refund_totals_are_calculated() { await using var db = CreditHistoryTest.Db(); var user = Guid.NewGuid(); db.AddRange(CreditHistoryTest.Entry(user, "Usage", -30), CreditHistoryTest.Entry(user, "Refund", 10)); await db.SaveChangesAsync(); var value = await CreditHistoryTest.Get(db, user); Assert.Equal(30, CreditHistoryTest.Int(value, "TotalUsed")); Assert.Equal(10, CreditHistoryTest.Int(value, "TotalRefunded")); }
}

public class FilterCreditHistoryUtcidTests
{
    [Fact(DisplayName="FUNC018-USR-UTCID01")] public async Task Purchase_filter_returns_purchases() => await CreditHistoryTest.AssertFilter("Purchase", 1);
    [Fact(DisplayName="FUNC018-USR-UTCID02")] public async Task Usage_filter_returns_usage() => await CreditHistoryTest.AssertFilter("Usage", 1);
    [Fact(DisplayName="FUNC018-USR-UTCID03")] public async Task Refund_filter_returns_refunds() => await CreditHistoryTest.AssertFilter("Refund", 1);
    [Fact(DisplayName="FUNC018-USR-UTCID04")] public async Task Unknown_filter_returns_empty() => await CreditHistoryTest.AssertFilter("Unknown", 0);
    [Fact(DisplayName="FUNC018-USR-UTCID05")] public async Task Pagination_is_normalized() { await using var db = CreditHistoryTest.Db(); var value = await CreditHistoryTest.Get(db, Guid.NewGuid(), null, 0, 1000); Assert.Equal(1, CreditHistoryTest.Int(value, "Page")); Assert.Equal(50, CreditHistoryTest.Int(value, "PageSize")); }
    [Fact(DisplayName="FUNC018-USR-UTCID06")] public async Task Second_page_contains_remaining_entry() { await using var db = CreditHistoryTest.Db(); var user = Guid.NewGuid(); db.AddRange(CreditHistoryTest.Entry(user, "Purchase", 1), CreditHistoryTest.Entry(user, "Purchase", 2), CreditHistoryTest.Entry(user, "Purchase", 3)); await db.SaveChangesAsync(); Assert.Single(CreditHistoryTest.Items(await CreditHistoryTest.Get(db, user, null, 2, 2))); }
}

internal static class CreditHistoryTest
{
    internal static PaymentDbContext Db() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    internal static CreditLedgerEntry Entry(Guid user, string type, int change, DateTime? time = null) => new() { LedgerId = Guid.NewGuid(), UserId = user, EntryType = type, CreditChange = change, BalanceAfter = 100, CreatedAt = time ?? DateTime.UtcNow };
    internal static async Task<object> Get(PaymentDbContext db, Guid user, string? type = null, int page = 1, int size = 10) { var ctx = new DefaultHttpContext(); ctx.Request.Headers["X-User-Id"] = user.ToString(); var result = await CreditLedgerEndpoints.HandleAsync(ctx, db, type, page, size); Assert.Equal(200, (result as IStatusCodeHttpResult)!.StatusCode); return ((IValueHttpResult)result).Value!; }
    internal static int Int(object value, string name) => (int)value.GetType().GetProperty(name)!.GetValue(value)!;
    internal static Guid GuidValue(object value, string name) => (Guid)value.GetType().GetProperty(name)!.GetValue(value)!;
    internal static List<object> Items(object value) => ((System.Collections.IEnumerable)value.GetType().GetProperty("Items")!.GetValue(value)!).Cast<object>().ToList();
    internal static async Task AssertFilter(string type, int expected) { await using var db = Db(); var user = Guid.NewGuid(); db.AddRange(Entry(user, "Purchase", 1), Entry(user, "Usage", -1), Entry(user, "Refund", 1)); await db.SaveChangesAsync(); var value = await Get(db, user, type); Assert.Equal(expected, Int(value, "TotalCount")); if (expected > 0) Assert.Equal(type, Items(value)[0].GetType().GetProperty("EntryType")!.GetValue(Items(value)[0])); }
}
