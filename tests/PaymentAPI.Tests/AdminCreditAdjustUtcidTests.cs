using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using PaymentAPI.Data;
using PaymentAPI.Endpoints;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class AdminCreditAdjustUtcidTests
{
    [Fact(DisplayName="FUNC149-ADM-UTCID01")]
    public async Task Add_writes_refund_entry_and_new_balance()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(40);
        Assert.Equal(200,Status(await AdminCreditEndpoints.AddAsync(new(account.Email,10," bonus "),auth.Object,db,ledger.Object,default)));
        var entry=Assert.Single(db.CreditLedgerEntries); Assert.Equal(account.AccountId,entry.UserId); Assert.Equal("Refund",entry.EntryType); Assert.Equal(10,entry.CreditChange); Assert.Equal(50,entry.BalanceAfter); Assert.Equal("bonus",entry.Description);
    }
    [Fact(DisplayName="FUNC149-ADM-UTCID02")]
    public async Task Add_uses_default_description()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(0);
        Assert.Equal(200,Status(await AdminCreditEndpoints.AddAsync(new(account.Email,1," "),auth.Object,db,ledger.Object,default)));
        Assert.Equal("Admin credit adjustment",Assert.Single(db.CreditLedgerEntries).Description);
    }
    [Fact(DisplayName="FUNC149-ADM-UTCID03")]
    public async Task Add_rejects_blank_email_without_dependencies()
    {
        await using var db=Db(); var auth=new Mock<IAuthAccountClient>(); var ledger=new Mock<ILedgerRepository>();
        Assert.Equal(400,Status(await AdminCreditEndpoints.AddAsync(new(" ",1,null),auth.Object,db,ledger.Object,default))); auth.VerifyNoOtherCalls(); ledger.VerifyNoOtherCalls();
    }
    [Fact(DisplayName="FUNC149-ADM-UTCID04")]
    public async Task Add_rejects_amount_outside_range()
    {
        await using var db=Db(); var auth=new Mock<IAuthAccountClient>(); var ledger=new Mock<ILedgerRepository>();
        Assert.Equal(400,Status(await AdminCreditEndpoints.AddAsync(new("a@b.com",0,null),auth.Object,db,ledger.Object,default)));
    }
    [Fact(DisplayName="FUNC149-ADM-UTCID05")]
    public async Task Add_accepts_one_million_boundary()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(2);
        Assert.Equal(200,Status(await AdminCreditEndpoints.AddAsync(new(account.Email,1_000_000,null),auth.Object,db,ledger.Object,default))); Assert.Equal(1_000_002,Assert.Single(db.CreditLedgerEntries).BalanceAfter);
    }
    [Fact(DisplayName="FUNC149-ADM-UTCID06")]
    public async Task Add_maps_auth_failure_and_missing_account()
    {
        await using var db=Db(); var auth=new Mock<IAuthAccountClient>(); var ledger=new Mock<ILedgerRepository>();
        auth.Setup(x=>x.FindByExactEmailAsync("fail@x.com",default)).ThrowsAsync(new HttpRequestException());
        Assert.Equal(502,Status(await AdminCreditEndpoints.AddAsync(new("fail@x.com",1,null),auth.Object,db,ledger.Object,default)));
        Assert.Equal(404,Status(await AdminCreditEndpoints.AddAsync(new("missing@x.com",1,null),auth.Object,db,ledger.Object,default)));
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID01")]
    public async Task Deduct_writes_negative_usage_entry()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(20);
        Assert.Equal(200,Status(await AdminCreditEndpoints.DeductAsync(new(account.Email,7," charge "),auth.Object,db,ledger.Object,default)));
        var entry=Assert.Single(db.CreditLedgerEntries); Assert.Equal("Usage",entry.EntryType); Assert.Equal(-7,entry.CreditChange); Assert.Equal(13,entry.BalanceAfter); Assert.Equal("charge",entry.Description);
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID02")]
    public async Task Deduct_exact_balance_reaches_zero()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(10);
        Assert.Equal(200,Status(await AdminCreditEndpoints.DeductAsync(new(account.Email,10,null),auth.Object,db,ledger.Object,default))); Assert.Equal(0,Assert.Single(db.CreditLedgerEntries).BalanceAfter);
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID03")]
    public async Task Deduct_rejects_insufficient_balance_without_entry()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(9);
        Assert.Equal(400,Status(await AdminCreditEndpoints.DeductAsync(new(account.Email,10,null),auth.Object,db,ledger.Object,default))); Assert.Empty(db.CreditLedgerEntries);
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID04")]
    public async Task Deduct_rejects_amount_above_maximum()
    {
        await using var db=Db(); var auth=new Mock<IAuthAccountClient>(); var ledger=new Mock<ILedgerRepository>();
        Assert.Equal(400,Status(await AdminCreditEndpoints.DeductAsync(new("a@b.com",1_000_001,null),auth.Object,db,ledger.Object,default))); auth.VerifyNoOtherCalls();
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID05")]
    public async Task Deduct_empty_guid_is_valid_account_boundary()
    {
        await using var db=Db(); var auth=new Mock<IAuthAccountClient>(); var ledger=new Mock<ILedgerRepository>(); var account=new ResolvedAccount(Guid.Empty,"empty@x.com");
        auth.Setup(x=>x.FindByExactEmailAsync(account.Email,default)).ReturnsAsync(account); ledger.Setup(x=>x.GetBalanceForUpdateAsync(Guid.Empty,default)).ReturnsAsync(1);
        Assert.Equal(200,Status(await AdminCreditEndpoints.DeductAsync(new(account.Email,1,null),auth.Object,db,ledger.Object,default))); Assert.Equal(Guid.Empty,Assert.Single(db.CreditLedgerEntries).UserId);
    }
    [Fact(DisplayName="FUNC150-ADM-UTCID06")]
    public async Task Deduct_maps_ledger_failure_to_server_error()
    {
        await using var db=Db(); var (auth,ledger,account)=Dependencies(10); ledger.Setup(x=>x.GetBalanceForUpdateAsync(account.AccountId,default)).ThrowsAsync(new InvalidOperationException("lock failed"));
        Assert.Equal(500,Status(await AdminCreditEndpoints.DeductAsync(new(account.Email,1,null),auth.Object,db,ledger.Object,default))); Assert.Empty(db.CreditLedgerEntries);
    }
    private static (Mock<IAuthAccountClient>,Mock<ILedgerRepository>,ResolvedAccount) Dependencies(int balance){var account=new ResolvedAccount(Guid.NewGuid(),"user@example.com");var auth=new Mock<IAuthAccountClient>();var ledger=new Mock<ILedgerRepository>();auth.Setup(x=>x.FindByExactEmailAsync(account.Email,default)).ReturnsAsync(account);ledger.Setup(x=>x.GetBalanceForUpdateAsync(account.AccountId,default)).ReturnsAsync(balance);return(auth,ledger,account);}
    private static PaymentDbContext Db()=>new(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static int Status(IResult result)=>Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;
}
