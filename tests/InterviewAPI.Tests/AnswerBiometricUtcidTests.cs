using System.Net;
using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.Helpers;
using InterviewAPI.Models;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class ValidateAnswerContentUtcidTests
{
 [Fact(DisplayName="FUNC046-USR-UTCID01")] public void Normal_answer_is_substantive()=>Assert.True(SubstantiveAnswerRules.IsSubstantive(new ChatMessage{SenderType="User",MessageType="Answer",Content="Dependency injection"}));
 [Fact(DisplayName="FUNC046-USR-UTCID02")] public void Null_content_is_not_substantive()=>Assert.False(SubstantiveAnswerRules.HasSubstantiveContent(null));
 [Fact(DisplayName="FUNC046-USR-UTCID03")] public void Empty_or_whitespace_content_is_not_substantive(){Assert.False(SubstantiveAnswerRules.HasSubstantiveContent(""));Assert.False(SubstantiveAnswerRules.HasSubstantiveContent("   "));}
 [Fact(DisplayName="FUNC046-USR-UTCID04")] public void Skipped_answer_is_not_substantive()=>Assert.False(SubstantiveAnswerRules.IsSubstantive(new ChatMessage{SenderType="User",MessageType="Answer",Content="text",AnswerReason="Skipped"}));
 [Fact(DisplayName="FUNC046-USR-UTCID05")] public void Content_length_boundaries_are_substantive(){Assert.True(SubstantiveAnswerRules.HasSubstantiveContent("x"));Assert.True(SubstantiveAnswerRules.HasSubstantiveContent(new string('x',10000)));}
 [Fact(DisplayName="FUNC046-USR-UTCID06")] public void Timed_out_or_non_user_message_is_not_substantive(){Assert.False(SubstantiveAnswerRules.IsSubstantive(new ChatMessage{SenderType="User",MessageType="Answer",Content="x",AnswerReason="TimedOut"}));Assert.False(SubstantiveAnswerRules.IsSubstantive(new ChatMessage{SenderType="AI",MessageType="Answer",Content="x"}));}
}

public class VerifyFaceEntryUtcidTests
{
 [Fact(DisplayName="FUNC047-USR-UTCID01")] public async Task Passed_entry_face_is_returned(){var(c,b,_,u)=VerifyTest.Create();var id=Guid.NewGuid();var frames=new List<IFormFile>{VerifyTest.File()};b.Setup(x=>x.VerifyFaceAsync(u,id,frames,It.IsAny<CancellationToken>(),"Entry")).ReturnsAsync(new VerificationResultResponse{Passed=true,Modality="Face"});Assert.Equal(200,VerifyTest.Status(await c.VerifyFace(id,frames,default)));}
 [Fact(DisplayName="FUNC047-USR-UTCID02")] public async Task Quality_rejection_returns_unprocessable(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyFaceAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<List<IFormFile>>(),It.IsAny<CancellationToken>(),It.IsAny<string>())).ThrowsAsync(new BiometricQualityException("blur"));Assert.Equal(422,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),[VerifyTest.File()],default)));}
 [Fact(DisplayName="FUNC047-USR-UTCID03")] public async Task Missing_identity_prevents_verification(){var(c,b,_,_)=VerifyTest.Create(false);Assert.Equal(503,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),[VerifyTest.File()],default)));b.VerifyNoOtherCalls();}
 [Fact(DisplayName="FUNC047-USR-UTCID04")] public async Task Missing_session_is_not_found(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyFaceAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<List<IFormFile>>(),It.IsAny<CancellationToken>(),It.IsAny<string>())).ThrowsAsync(new KeyNotFoundException("session"));Assert.Equal(404,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),[VerifyTest.File()],default)));}
 [Fact(DisplayName="FUNC047-USR-UTCID05")] public async Task Frame_count_boundaries_are_forwarded(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyFaceAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<List<IFormFile>>(),It.IsAny<CancellationToken>(),It.IsAny<string>())).ReturnsAsync(new VerificationResultResponse());Assert.Equal(200,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),[VerifyTest.File()],default)));Assert.Equal(200,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),Enumerable.Range(0,10).Select(_=>VerifyTest.File()).ToList(),default)));}
 [Fact(DisplayName="FUNC047-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyFaceAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<List<IFormFile>>(),It.IsAny<CancellationToken>(),It.IsAny<string>())).ThrowsAsync(new Exception());Assert.Equal(503,VerifyTest.Status(await c.VerifyFace(Guid.NewGuid(),[VerifyTest.File()],default)));}
}

public class ValidateEntryUtcidTests
{
 [Fact(DisplayName="FUNC048-USR-UTCID01")] public async Task Allowed_entry_gate_is_returned(){var(c,b,_,u)=VerifyTest.Create();var id=Guid.NewGuid();b.Setup(x=>x.GetEntryGateStatusAsync(u,id,It.IsAny<CancellationToken>())).ReturnsAsync(new EntryGateStatusResponse{Allowed=true});Assert.Equal(200,VerifyTest.Status(await c.GetEntryGateStatus(id,default)));}
 [Fact(DisplayName="FUNC048-USR-UTCID02")] public async Task Denied_entry_gate_is_valid_result(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.GetEntryGateStatusAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EntryGateStatusResponse{Allowed=false,Reason="face_required"});Assert.Equal(200,VerifyTest.Status(await c.GetEntryGateStatus(Guid.NewGuid(),default)));}
 [Fact(DisplayName="FUNC048-USR-UTCID03")] public async Task Missing_identity_prevents_lookup(){var(c,b,_,_)=VerifyTest.Create(false);Assert.Equal(503,VerifyTest.Status(await c.GetEntryGateStatus(Guid.NewGuid(),default)));b.VerifyNoOtherCalls();}
 [Fact(DisplayName="FUNC048-USR-UTCID04")] public async Task Missing_session_is_not_found(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.GetEntryGateStatusAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());Assert.Equal(404,VerifyTest.Status(await c.GetEntryGateStatus(Guid.NewGuid(),default)));}
 [Fact(DisplayName="FUNC048-USR-UTCID05")] public async Task Guid_boundaries_are_forwarded(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.GetEntryGateStatusAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ReturnsAsync(new EntryGateStatusResponse());Assert.Equal(200,VerifyTest.Status(await c.GetEntryGateStatus(Guid.Empty,default)));Assert.Equal(200,VerifyTest.Status(await c.GetEntryGateStatus(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),default)));}
 [Fact(DisplayName="FUNC048-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.GetEntryGateStatusAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());Assert.Equal(503,VerifyTest.Status(await c.GetEntryGateStatus(Guid.NewGuid(),default)));}
}

public class InSessionFaceUtcidTests
{
 [Fact(DisplayName="FUNC049-USR-UTCID01")] public async Task Passed_check_is_returned(){var(c,b,_,u)=VerifyTest.Create();var s=Guid.NewGuid();var check=Guid.NewGuid();var f=VerifyTest.File();b.Setup(x=>x.VerifyInSessionAsync(u,s,check,f,It.IsAny<CancellationToken>())).ReturnsAsync(new InSessionVerifyResponse{Passed=true});Assert.Equal(200,VerifyTest.Status(await c.VerifyInSession(s,check,f,default)));}
 [Fact(DisplayName="FUNC049-USR-UTCID02")] public async Task Failed_identity_check_is_valid_result(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyInSessionAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ReturnsAsync(new InSessionVerifyResponse{Passed=false,StrikeCount=1});Assert.Equal(200,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC049-USR-UTCID03")] public async Task Missing_identity_prevents_check(){var(c,b,_,_)=VerifyTest.Create(false);Assert.Equal(503,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));b.VerifyNoOtherCalls();}
 [Fact(DisplayName="FUNC049-USR-UTCID04")] public async Task Missing_check_is_not_found(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyInSessionAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());Assert.Equal(404,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC049-USR-UTCID05")] public async Task Check_id_boundaries_are_forwarded(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyInSessionAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ReturnsAsync(new InSessionVerifyResponse());Assert.Equal(200,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.Empty,VerifyTest.File(),default)));Assert.Equal(200,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC049-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyInSessionAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());Assert.Equal(503,VerifyTest.Status(await c.VerifyInSession(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
}

public class VoiceAnswerIdentityUtcidTests
{
 [Fact(DisplayName="FUNC050-USR-UTCID01")] public async Task Matching_voice_returns_transcript(){var(c,b,_,u)=VerifyTest.Create();var s=Guid.NewGuid();var check=Guid.NewGuid();var f=VerifyTest.File();b.Setup(x=>x.VerifyVoiceAnswerAsync(u,s,check,f,It.IsAny<CancellationToken>())).ReturnsAsync(new VoiceAnswerVerifyResponse{Text="answer",VoiceMatched=true});Assert.Equal(200,VerifyTest.Status(await c.VerifyVoiceAnswer(s,check,f,default)));}
 [Fact(DisplayName="FUNC050-USR-UTCID02")] public async Task Mismatch_strike_is_valid_result(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyVoiceAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ReturnsAsync(new VoiceAnswerVerifyResponse{VoiceMatched=false,StrikeCount=1});Assert.Equal(200,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC050-USR-UTCID03")] public async Task Missing_identity_prevents_check(){var(c,b,_,_)=VerifyTest.Create(false);Assert.Equal(503,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));b.VerifyNoOtherCalls();}
 [Fact(DisplayName="FUNC050-USR-UTCID04")] public async Task Missing_check_is_not_found(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyVoiceAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException());Assert.Equal(404,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC050-USR-UTCID05")] public async Task Check_id_boundaries_are_forwarded(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyVoiceAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ReturnsAsync(new VoiceAnswerVerifyResponse());Assert.Equal(200,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.Empty,VerifyTest.File(),default)));Assert.Equal(200,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),VerifyTest.File(),default)));}
 [Fact(DisplayName="FUNC050-USR-UTCID06")] public async Task Dependency_failure_returns_service_unavailable(){var(c,b,_,_)=VerifyTest.Create();b.Setup(x=>x.VerifyVoiceAnswerAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<IFormFile>(),It.IsAny<CancellationToken>())).ThrowsAsync(new Exception());Assert.Equal(503,VerifyTest.Status(await c.VerifyVoiceAnswer(Guid.NewGuid(),Guid.NewGuid(),VerifyTest.File(),default)));}
}

internal static class VerifyTest
{
 internal static(SessionVerificationController,Mock<IBiometricVerificationService>,Mock<ISessionService>,Guid)Create(bool auth=true){var b=new Mock<IBiometricVerificationService>();var s=new Mock<ISessionService>();var u=Guid.NewGuid();var ctx=new DefaultHttpContext();if(auth)ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,u.ToString())]));var c=new SessionVerificationController(b.Object,s.Object,NullLogger<SessionVerificationController>.Instance){ControllerContext=new ControllerContext{HttpContext=ctx}};return(c,b,s,u);}
 internal static int? Status(IActionResult r)=>(r as ObjectResult)?.StatusCode;
 internal static IFormFile File(){var bytes=new byte[]{1};return new FormFile(new MemoryStream(bytes),0,1,"file","sample.bin"){Headers=new HeaderDictionary(),ContentType="application/octet-stream"};}
}
