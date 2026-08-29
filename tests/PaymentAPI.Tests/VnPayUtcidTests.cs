using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PaymentAPI.Endpoints;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class CreateVnPayOrderUtcidTests
{
 [Fact(DisplayName="FUNC079-USR-UTCID01")] public Task Valid_order_returns_url()=>VnPayTest.Order(1);
 [Fact(DisplayName="FUNC079-USR-UTCID02")] public Task Invalid_identity_is_bad_request()=>VnPayTest.Order(2);
 [Fact(DisplayName="FUNC079-USR-UTCID03")] public Task Invalid_json_is_bad_request()=>VnPayTest.Order(3);
 [Fact(DisplayName="FUNC079-USR-UTCID04")] public Task Missing_package_is_not_found()=>VnPayTest.Order(4);
 [Fact(DisplayName="FUNC079-USR-UTCID05")] public Task Client_ip_is_forwarded()=>VnPayTest.Order(5);
 [Fact(DisplayName="FUNC079-USR-UTCID06")] public Task Dependency_failure_is_problem()=>VnPayTest.Order(6);
}
public class ProcessVnPayIpnUtcidTests
{
 [Fact(DisplayName="FUNC080-USR-UTCID01")] public Task Success_response()=>VnPayTest.Ipn("00","Confirm Success");
 [Fact(DisplayName="FUNC080-USR-UTCID02")] public Task Order_not_found_response()=>VnPayTest.Ipn("01","Order not found");
 [Fact(DisplayName="FUNC080-USR-UTCID03")] public Task Invalid_signature_response()=>VnPayTest.Ipn("97","Invalid signature");
 [Fact(DisplayName="FUNC080-USR-UTCID04")] public Task Unknown_error_response()=>VnPayTest.Ipn("99","Unknown error");
 [Fact(DisplayName="FUNC080-USR-UTCID05")] public Task Query_is_forwarded()=>VnPayTest.IpnQuery();
 [Fact(DisplayName="FUNC080-USR-UTCID06")] public Task Dependency_failure_propagates()=>VnPayTest.IpnError();
}
public class HandleVnPayReturnUtcidTests
{
 [Fact(DisplayName="FUNC081-USR-UTCID01")] public Task Paid_redirect()=>VnPayTest.Return("/payment/success");
 [Fact(DisplayName="FUNC081-USR-UTCID02")] public Task Failed_redirect()=>VnPayTest.Return("/payment/failed");
 [Fact(DisplayName="FUNC081-USR-UTCID03")] public Task Cancelled_redirect()=>VnPayTest.Return("/payment/cancelled");
 [Fact(DisplayName="FUNC081-USR-UTCID04")] public Task Empty_query_is_forwarded()=>VnPayTest.ReturnQuery(false);
 [Fact(DisplayName="FUNC081-USR-UTCID05")] public Task Callback_query_is_forwarded()=>VnPayTest.ReturnQuery(true);
 [Fact(DisplayName="FUNC081-USR-UTCID06")] public Task Dependency_failure_propagates()=>VnPayTest.ReturnError();
}

internal static class VnPayTest
{
 internal static async Task Order(int scenario){var service=new Mock<IVnPayService>();var ctx=Context();var user=Guid.NewGuid();var package=Guid.NewGuid();ctx.Request.Headers["X-User-Id"]=scenario==2?"invalid":user.ToString();ctx.Request.Body=new MemoryStream(Encoding.UTF8.GetBytes(scenario==3?"{bad":$"{{\"packageId\":\"{package}\"}}"));if(scenario==5)ctx.Connection.RemoteIpAddress=IPAddress.Parse("10.0.0.8");if(scenario is 1 or 5)service.Setup(x=>x.CreateOrderUrlAsync(user,package,scenario==5?"10.0.0.8":"127.0.0.1")).ReturnsAsync("https://vnpay/order");if(scenario==4)service.Setup(x=>x.CreateOrderUrlAsync(user,package,It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("package"));if(scenario==6)service.Setup(x=>x.CreateOrderUrlAsync(user,package,It.IsAny<string>())).ThrowsAsync(new Exception("gateway"));var result=await VnPayEndpoints.CreateOrderAsync(ctx,service.Object);Assert.Equal(scenario switch{1 or 5=>200,2 or 3=>400,4=>404,_=>500},((IStatusCodeHttpResult)result).StatusCode);}
 internal static async Task Ipn(string code,string message){var s=new Mock<IVnPayService>();var ctx=Context();s.Setup(x=>x.ProcessIpnAsync(It.IsAny<IReadOnlyDictionary<string,string>>())).ReturnsAsync(new VnPayIpnResult(code,message));var result=await VnPayEndpoints.ProcessIpnAsync(ctx.Request,s.Object);var value=((IValueHttpResult)result).Value!;Assert.Equal(code,value.GetType().GetProperty("RspCode")!.GetValue(value));Assert.Equal(message,value.GetType().GetProperty("Message")!.GetValue(value));}
 internal static async Task IpnQuery(){var s=new Mock<IVnPayService>();var ctx=Context("?vnp_TxnRef=ABC&vnp_Amount=100");s.Setup(x=>x.ProcessIpnAsync(It.Is<IReadOnlyDictionary<string,string>>(q=>q["vnp_TxnRef"]=="ABC"&&q["vnp_Amount"]=="100"))).ReturnsAsync(new VnPayIpnResult("00","ok"));await VnPayEndpoints.ProcessIpnAsync(ctx.Request,s.Object);s.VerifyAll();}
 internal static async Task IpnError(){var s=new Mock<IVnPayService>();var ctx=Context();s.Setup(x=>x.ProcessIpnAsync(It.IsAny<IReadOnlyDictionary<string,string>>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>VnPayEndpoints.ProcessIpnAsync(ctx.Request,s.Object));}
 internal static async Task Return(string url){var s=new Mock<IVnPayService>();var ctx=Context();s.Setup(x=>x.ProcessReturnAsync(It.IsAny<IReadOnlyDictionary<string,string>>())).ReturnsAsync(url);var result=await VnPayEndpoints.ProcessReturnAsync(ctx.Request,s.Object);Assert.Equal("RedirectHttpResult",result.GetType().Name);Assert.Equal(url,result.GetType().GetProperty("Url")!.GetValue(result));Assert.False((bool)result.GetType().GetProperty("Permanent")!.GetValue(result)!);}
 internal static async Task ReturnQuery(bool populated){var s=new Mock<IVnPayService>();var ctx=Context(populated?"?vnp_ResponseCode=00":null);s.Setup(x=>x.ProcessReturnAsync(It.Is<IReadOnlyDictionary<string,string>>(q=>populated?q["vnp_ResponseCode"]=="00":q.Count==0))).ReturnsAsync("/done");await VnPayEndpoints.ProcessReturnAsync(ctx.Request,s.Object);s.VerifyAll();}
 internal static async Task ReturnError(){var s=new Mock<IVnPayService>();var ctx=Context();s.Setup(x=>x.ProcessReturnAsync(It.IsAny<IReadOnlyDictionary<string,string>>())).ThrowsAsync(new InvalidOperationException("db"));await Assert.ThrowsAsync<InvalidOperationException>(()=>VnPayEndpoints.ProcessReturnAsync(ctx.Request,s.Object));}
 private static DefaultHttpContext Context(string? query=null){var ctx=new DefaultHttpContext();ctx.RequestServices=new ServiceCollection().AddLogging().BuildServiceProvider();if(query is not null)ctx.Request.QueryString=new QueryString(query);return ctx;}
}
