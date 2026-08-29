using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs.Session;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class TextToSpeechUtcidTests
{
    [Fact(DisplayName="FUNC041-USR-UTCID01")] public async Task Returns_mpeg_audio(){var(c,_,ai,_,_)=MediaTest.Create();var bytes=new byte[]{1,2,3};ai.Setup(x=>x.SynthesizeSpeechAsync("question",It.IsAny<CancellationToken>())).ReturnsAsync(bytes);var file=Assert.IsType<FileContentResult>(await c.TextToSpeech(new TtsRequest{Text="question"},default));Assert.Equal("audio/mpeg",file.ContentType);Assert.Equal(bytes,file.FileContents);}
    [Fact(DisplayName="FUNC041-USR-UTCID02")] public async Task Empty_text_is_rejected_without_ai_call(){var(c,_,ai,_,_)=MediaTest.Create();Assert.IsType<BadRequestObjectResult>(await c.TextToSpeech(new TtsRequest{Text=""},default));ai.Verify(x=>x.SynthesizeSpeechAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact(DisplayName="FUNC041-USR-UTCID03")] public async Task Whitespace_text_is_rejected(){var(c,_,_,_,_)=MediaTest.Create();Assert.IsType<BadRequestObjectResult>(await c.TextToSpeech(new TtsRequest{Text="   "},default));}
    [Fact(DisplayName="FUNC041-USR-UTCID04")] public async Task Empty_ai_audio_returns_bad_gateway(){var(c,_,ai,_,_)=MediaTest.Create();ai.Setup(x=>x.SynthesizeSpeechAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync([]);Assert.Equal(502,MediaTest.Status(await c.TextToSpeech(new TtsRequest{Text="q"},default)));}
    [Fact(DisplayName="FUNC041-USR-UTCID05")] public async Task Text_length_boundaries_are_forwarded(){var(c,_,ai,_,_)=MediaTest.Create();ai.Setup(x=>x.SynthesizeSpeechAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync([1]);Assert.IsType<FileContentResult>(await c.TextToSpeech(new TtsRequest{Text="x"},default));Assert.IsType<FileContentResult>(await c.TextToSpeech(new TtsRequest{Text=new string('x',10000)},default));ai.Verify(x=>x.SynthesizeSpeechAsync(It.IsAny<string>(),It.IsAny<CancellationToken>()),Times.Exactly(2));}
    [Fact(DisplayName="FUNC041-USR-UTCID06")] public async Task Ai_failure_returns_service_unavailable(){var(c,_,ai,_,_)=MediaTest.Create();ai.Setup(x=>x.SynthesizeSpeechAsync(It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());Assert.Equal(503,MediaTest.Status(await c.TextToSpeech(new TtsRequest{Text="q"},default)));}
}

public class SpeechToTextUtcidTests
{
    [Fact(DisplayName="FUNC042-USR-UTCID01")] public async Task Transcribes_audio_bytes_and_metadata(){var(c,_,ai,_,_)=MediaTest.Create();var file=MediaTest.File("answer.webm","audio/webm",[1,2,3]);ai.Setup(x=>x.TranscribeAsync(It.Is<byte[]>(b=>b.SequenceEqual(new byte[]{1,2,3})),"answer.webm","audio/webm",It.IsAny<CancellationToken>())).ReturnsAsync("hello");var ok=Assert.IsType<OkObjectResult>(await c.SpeechToText(file,default));Assert.Equal("hello",Assert.IsType<InterviewAPI.DTOs.ApiResponse<SttResponse>>(ok.Value).Data!.Text);}
    [Fact(DisplayName="FUNC042-USR-UTCID02")] public async Task Null_audio_is_rejected(){var(c,_,ai,_,_)=MediaTest.Create();Assert.IsType<BadRequestObjectResult>(await c.SpeechToText(null!,default));ai.VerifyNoOtherCalls();}
    [Fact(DisplayName="FUNC042-USR-UTCID03")] public async Task Empty_audio_is_rejected(){var(c,_,_,_,_)=MediaTest.Create();Assert.IsType<BadRequestObjectResult>(await c.SpeechToText(MediaTest.File("a.webm","audio/webm",[]),default));}
    [Fact(DisplayName="FUNC042-USR-UTCID04")] public async Task Ai_rejection_returns_service_unavailable(){var(c,_,ai,_,_)=MediaTest.Create();ai.Setup(x=>x.TranscribeAsync(It.IsAny<byte[]>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());Assert.Equal(503,MediaTest.Status(await c.SpeechToText(MediaTest.File("a.webm","audio/webm",[1]),default)));}
    [Fact(DisplayName="FUNC042-USR-UTCID05")] public async Task Missing_metadata_uses_safe_defaults(){var(c,_,ai,_,_)=MediaTest.Create();var file=MediaTest.File("","",[1]);ai.Setup(x=>x.TranscribeAsync(It.IsAny<byte[]>(),"answer.webm","audio/webm",It.IsAny<CancellationToken>())).ReturnsAsync("x");Assert.Equal(200,MediaTest.Status(await c.SpeechToText(file,default)));}
    [Fact(DisplayName="FUNC042-USR-UTCID06")] public async Task Transport_failure_returns_service_unavailable(){var(c,_,ai,_,_)=MediaTest.Create();ai.Setup(x=>x.TranscribeAsync(It.IsAny<byte[]>(),It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());Assert.Equal(503,MediaTest.Status(await c.SpeechToText(MediaTest.File("a.webm","audio/webm",[1]),default)));}
}

public class HintUtcidTests
{
    [Fact(DisplayName="FUNC043-USR-UTCID01")] public async Task Returns_hint_for_owned_question(){var(c,s,_,_,u)=MediaTest.Create();var session=Guid.NewGuid();var q=Guid.NewGuid();s.Setup(x=>x.HintAsync(u,session,q,It.IsAny<CancellationToken>())).ReturnsAsync(new HintResponse{Hint="Consider complexity"});Assert.Equal(200,MediaTest.Status(await c.Hint(session,new HintRequest{SessionQuestionId=q},default)));}
    [Fact(DisplayName="FUNC043-USR-UTCID02")] public async Task Missing_question_returns_not_found(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.HintAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("Question"));Assert.IsType<NotFoundObjectResult>(await c.Hint(Guid.NewGuid(),new HintRequest(),default));}
    [Fact(DisplayName="FUNC043-USR-UTCID03")] public async Task Missing_identity_returns_service_unavailable_without_service_call(){var(c,s,_,_,_)=MediaTest.Create(false);Assert.Equal(503,MediaTest.Status(await c.Hint(Guid.NewGuid(),new HintRequest(),default)));s.Verify(x=>x.HintAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>()),Times.Never);}
    [Fact(DisplayName="FUNC043-USR-UTCID04")] public async Task Used_hint_guard_returns_conflict(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.HintAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new SessionGuardException(409,"hint_already_used","used"));Assert.Equal(409,MediaTest.Status(await c.Hint(Guid.NewGuid(),new HintRequest(),default)));}
    [Fact(DisplayName="FUNC043-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.HintAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new HintResponse());Assert.Equal(200,MediaTest.Status(await c.Hint(Guid.Empty,new HintRequest{SessionQuestionId=Guid.Empty},default)));Assert.Equal(200,MediaTest.Status(await c.Hint(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),new HintRequest{SessionQuestionId=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff")},default)));}
    [Fact(DisplayName="FUNC043-USR-UTCID06")] public async Task Ai_failure_returns_service_unavailable(){var(c,s,_,_,_)=MediaTest.Create();s.Setup(x=>x.HintAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());Assert.Equal(503,MediaTest.Status(await c.Hint(Guid.NewGuid(),new HintRequest(),default)));}
}

public class CreditBalanceUtcidTests
{
    [Fact(DisplayName="FUNC044-USR-UTCID01")] public async Task Returns_known_positive_balance(){var(c,_,_,pay,u)=MediaTest.Create();pay.Setup(x=>x.GetBalanceAsync(u,It.IsAny<CancellationToken>())).ReturnsAsync(CreditBalanceResult.Known(75));Assert.Equal(200,MediaTest.Status(await c.GetCreditBalance(default)));}
    [Fact(DisplayName="FUNC044-USR-UTCID02")] public async Task Known_zero_balance_is_valid(){var(c,_,_,pay,_)=MediaTest.Create();pay.Setup(x=>x.GetBalanceAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(CreditBalanceResult.Known(0));Assert.Equal(200,MediaTest.Status(await c.GetCreditBalance(default)));}
    [Fact(DisplayName="FUNC044-USR-UTCID03")] public async Task Missing_identity_returns_internal_error_without_payment_call(){var(c,_,_,pay,_)=MediaTest.Create(false);Assert.Equal(500,MediaTest.Status(await c.GetCreditBalance(default)));pay.VerifyNoOtherCalls();}
    [Fact(DisplayName="FUNC044-USR-UTCID04")] public async Task Unknown_balance_returns_service_unavailable(){var(c,_,_,pay,_)=MediaTest.Create();pay.Setup(x=>x.GetBalanceAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(CreditBalanceResult.Unknown("offline"));Assert.Equal(503,MediaTest.Status(await c.GetCreditBalance(default)));}
    [Fact(DisplayName="FUNC044-USR-UTCID05")] public async Task Maximum_integer_balance_is_returned(){var(c,_,_,pay,_)=MediaTest.Create();pay.Setup(x=>x.GetBalanceAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(CreditBalanceResult.Known(int.MaxValue));Assert.Equal(200,MediaTest.Status(await c.GetCreditBalance(default)));}
    [Fact(DisplayName="FUNC044-USR-UTCID06")] public async Task Payment_exception_returns_internal_error(){var(c,_,_,pay,_)=MediaTest.Create();pay.Setup(x=>x.GetBalanceAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException());Assert.Equal(500,MediaTest.Status(await c.GetCreditBalance(default)));}
}

internal static class MediaTest
{
    internal static (InterviewSessionController Controller,Mock<ISessionService> Service,Mock<IAiServiceClient> Ai,Mock<IPaymentApiClient> Payment,Guid UserId) Create(bool auth=true){var s=new Mock<ISessionService>();var ai=new Mock<IAiServiceClient>();var pay=new Mock<IPaymentApiClient>();var user=Guid.NewGuid();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,user.ToString())]));var c=new InterviewSessionController(s.Object,ai.Object,pay.Object,NullLogger<InterviewSessionController>.Instance){ControllerContext=new ControllerContext{HttpContext=ctx}};return(c,s,ai,pay,user);}
    internal static int? Status(IActionResult result)=>(result as ObjectResult)?.StatusCode;
    internal static IFormFile File(string name,string type,byte[] bytes){var stream=new MemoryStream(bytes);return new FormFile(stream,0,bytes.Length,"audio",name){Headers=new HeaderDictionary(),ContentType=type};}
}
