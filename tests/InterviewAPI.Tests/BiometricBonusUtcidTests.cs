using System.Net;
using InterviewAPI.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InterviewAPI.Tests;

public class BiometricBonusUtcidTests
{
 [Fact(DisplayName="FUNC051-USR-UTCID01")] public async Task First_eligible_enrollment_awards_bonus(){var h=new Handler(HttpStatusCode.OK,"{\"success\":true,\"awarded\":true,\"balanceAfter\":100}");var r=await Client(h).AwardCreditAsync(Guid.NewGuid(),100,Guid.NewGuid(),"Biometric bonus",default);Assert.True(r.Success);Assert.True(r.Awarded);Assert.Equal(100,r.BalanceAfter);Assert.Equal("/api/internal/credit/award",h.Path);}
 [Fact(DisplayName="FUNC051-USR-UTCID02")] public async Task Already_awarded_is_successful_idempotent_noop(){var r=await Client(new Handler(HttpStatusCode.OK,"{\"success\":true,\"awarded\":false,\"balanceAfter\":100}")).AwardCreditAsync(Guid.NewGuid(),100,Guid.NewGuid(),"bonus",default);Assert.True(r.Success);Assert.False(r.Awarded);}
 [Fact(DisplayName="FUNC051-USR-UTCID03")] public async Task Empty_response_becomes_failed_result(){var r=await Client(new Handler(HttpStatusCode.OK,"")).AwardCreditAsync(Guid.NewGuid(),100,Guid.NewGuid(),"bonus",default);Assert.False(r.Success);Assert.Contains("Error",r.Message);}
 [Fact(DisplayName="FUNC051-USR-UTCID04")] public async Task Payment_http_failure_does_not_award(){var r=await Client(new Handler(HttpStatusCode.ServiceUnavailable,"down")).AwardCreditAsync(Guid.NewGuid(),100,Guid.NewGuid(),"bonus",default);Assert.False(r.Success);Assert.False(r.Awarded);Assert.Contains("ServiceUnavailable",r.Message);}
 [Fact(DisplayName="FUNC051-USR-UTCID05")] public async Task Amount_and_identifier_boundaries_are_serialized(){var h1=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Client(h1).AwardCreditAsync(Guid.Empty,0,Guid.Empty,"",default);Assert.Contains("\"amount\":0",h1.Body,StringComparison.OrdinalIgnoreCase);var h2=new Handler(HttpStatusCode.OK,"{\"success\":true}");await Client(h2).AwardCreditAsync(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),int.MaxValue,Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),"max",default);Assert.Contains(int.MaxValue.ToString(),h2.Body);}
 [Fact(DisplayName="FUNC051-USR-UTCID06")] public async Task Transport_exception_does_not_award(){var r=await Client(new Thrower()).AwardCreditAsync(Guid.NewGuid(),100,Guid.NewGuid(),"bonus",default);Assert.False(r.Success);Assert.False(r.Awarded);Assert.Contains("offline",r.Message);}
 private static PaymentApiClient Client(HttpMessageHandler h)=>new(new HttpClient(h){BaseAddress=new Uri("http://payment")},NullLogger<PaymentApiClient>.Instance);
 private sealed class Handler(HttpStatusCode code,string response):HttpMessageHandler{public string? Path{get;private set;}public string Body{get;private set;}="";protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Path=request.RequestUri!.AbsolutePath;Body=request.Content is null?"":await request.Content.ReadAsStringAsync(ct);return new HttpResponseMessage(code){Content=new StringContent(response)};}}
 private sealed class Thrower:HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>throw new HttpRequestException("offline");}
}
