using System.Net;
using InterviewAPI.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InterviewAPI.Tests;

public class ChargeCreditUtcidTests
{
    [Fact(DisplayName="FUNC045-USR-UTCID01")] public async Task Successful_charge_returns_new_balance(){var handler=new Handler(HttpStatusCode.OK,"{\"success\":true,\"balanceAfter\":90,\"amountDeducted\":10}");var client=Create(handler);var result=await client.DeductCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"Interview",default);Assert.True(result.Success);Assert.True(result.ServiceAvailable);Assert.Equal(90,result.BalanceAfter);Assert.Equal(10,result.AmountDeducted);Assert.Equal("/api/internal/credit/deduct",handler.Path);}
    [Fact(DisplayName="FUNC045-USR-UTCID02")] public async Task Insufficient_credit_business_response_is_preserved(){var client=Create(new Handler(HttpStatusCode.OK,"{\"success\":false,\"serviceAvailable\":true,\"message\":\"Insufficient credits\"}"));var result=await client.DeductCreditAsync(Guid.NewGuid(),50,Guid.NewGuid(),"Interview",default);Assert.False(result.Success);Assert.True(result.ServiceAvailable);Assert.Equal("Insufficient credits",result.Message);}
    [Fact(DisplayName="FUNC045-USR-UTCID03")] public async Task Empty_success_response_is_classified_as_unavailable(){var client=Create(new Handler(HttpStatusCode.OK,""));var result=await client.DeductCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"Interview",default);Assert.False(result.Success);Assert.False(result.ServiceAvailable);Assert.Contains("Error",result.Message);}
    [Fact(DisplayName="FUNC045-USR-UTCID04")] public async Task Non_success_http_status_marks_service_unavailable(){var client=Create(new Handler(HttpStatusCode.ServiceUnavailable,"down"));var result=await client.DeductCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"Interview",default);Assert.False(result.Success);Assert.False(result.ServiceAvailable);Assert.Contains("ServiceUnavailable",result.Message);}
    [Fact(DisplayName="FUNC045-USR-UTCID05")] public async Task Amount_boundaries_are_serialized_exactly(){var zero=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Create(zero).DeductCreditAsync(Guid.Empty,0,Guid.Empty,"",default);Assert.Contains("\"amount\":0",zero.Body,StringComparison.OrdinalIgnoreCase);var max=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Create(max).DeductCreditAsync(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),int.MaxValue,Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),"max",default);Assert.Contains(int.MaxValue.ToString(),max.Body);}
    [Fact(DisplayName="FUNC045-USR-UTCID06")] public async Task Transport_exception_marks_service_unavailable(){var client=Create(new ThrowingHandler());var result=await client.DeductCreditAsync(Guid.NewGuid(),10,Guid.NewGuid(),"Interview",default);Assert.False(result.Success);Assert.False(result.ServiceAvailable);Assert.Contains("offline",result.Message);}

    private static PaymentApiClient Create(HttpMessageHandler handler)=>new(new HttpClient(handler){BaseAddress=new Uri("http://payment")},NullLogger<PaymentApiClient>.Instance);
    private sealed class Handler(HttpStatusCode status,string responseBody):HttpMessageHandler{public string? Path{get;private set;}public string Body{get;private set;}="";protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Path=request.RequestUri!.AbsolutePath;Body=request.Content is null?"":await request.Content.ReadAsStringAsync(cancellationToken);return new HttpResponseMessage(status){Content=new StringContent(responseBody)};}}
    private sealed class ThrowingHandler:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>throw new HttpRequestException("offline");}
}
