using System.Security.Claims;
using InterviewAPI.Clients;
using InterviewAPI.Controllers;
using InterviewAPI.DTOs;
using InterviewAPI.DTOs.Biometrics;
using InterviewAPI.Services;
using InterviewAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InterviewAPI.Tests;

public class EnrollFaceUtcidTests
{
    [Fact(DisplayName="FUNC011-USR-UTCID01")] public async Task Valid_face_is_enrolled() => await ProfileBiometricTest.AssertEnrollment("face", 200);
    [Fact(DisplayName="FUNC011-USR-UTCID02")] public async Task Poor_quality_face_is_rejected() => await ProfileBiometricTest.AssertEnrollment("face", 422, new BiometricQualityException("No face detected"));
    [Fact(DisplayName="FUNC011-USR-UTCID03")] public async Task Identity_mismatch_conflicts() => await ProfileBiometricTest.AssertEnrollment("face", 409, new BiometricIdentityMismatchException("Face", .1, .8));
    [Fact(DisplayName="FUNC011-USR-UTCID04")] public async Task Missing_user_claim_is_unavailable() => await ProfileBiometricTest.AssertMissingIdentity(false);
    [Fact(DisplayName="FUNC011-USR-UTCID05")] public async Task File_size_boundaries_are_forwarded() { await ProfileBiometricTest.AssertFileLength("face", 1); await ProfileBiometricTest.AssertFileLength("face", 10_000); }
    [Fact(DisplayName="FUNC011-USR-UTCID06")] public async Task Face_dependency_failure_is_unavailable() => await ProfileBiometricTest.AssertEnrollment("face", 503, new InvalidOperationException("dependency"));
}

public class BiometricStatusUtcidTests
{
    [Fact(DisplayName="FUNC012-USR-UTCID01")] public async Task Both_modalities_are_reported() => await ProfileBiometricTest.AssertStatus(true, true);
    [Fact(DisplayName="FUNC012-USR-UTCID02")] public async Task Face_only_is_reported() => await ProfileBiometricTest.AssertStatus(true, false);
    [Fact(DisplayName="FUNC012-USR-UTCID03")] public async Task Voice_only_is_reported() => await ProfileBiometricTest.AssertStatus(false, true);
    [Fact(DisplayName="FUNC012-USR-UTCID04")] public async Task No_enrollment_is_reported() => await ProfileBiometricTest.AssertStatus(false, false);
    [Fact(DisplayName="FUNC012-USR-UTCID05")] public async Task Missing_status_identity_is_unavailable() { var (c, s, _) = ProfileBiometricTest.Create(false); Assert.Equal(503, ProfileBiometricTest.Status(await c.GetStatus(default))); s.VerifyNoOtherCalls(); }
    [Fact(DisplayName="FUNC012-USR-UTCID06")] public async Task Status_dependency_failure_is_unavailable() { var (c, s, _) = ProfileBiometricTest.Create(); s.Setup(x => x.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception()); Assert.Equal(503, ProfileBiometricTest.Status(await c.GetStatus(default))); }
}

public class EnrollVoiceUtcidTests
{
    [Fact(DisplayName="FUNC013-USR-UTCID01")] public async Task Valid_voice_is_enrolled() => await ProfileBiometricTest.AssertEnrollment("voice", 200);
    [Fact(DisplayName="FUNC013-USR-UTCID02")] public async Task Poor_quality_voice_is_rejected() => await ProfileBiometricTest.AssertEnrollment("voice", 422, new BiometricQualityException("Audio too short"));
    [Fact(DisplayName="FUNC013-USR-UTCID03")] public async Task Voice_identity_mismatch_conflicts() => await ProfileBiometricTest.AssertEnrollment("voice", 409, new BiometricIdentityMismatchException("Voice", .2, .8));
    [Fact(DisplayName="FUNC013-USR-UTCID04")] public async Task Missing_voice_user_claim_is_unavailable() => await ProfileBiometricTest.AssertMissingIdentity(true);
    [Fact(DisplayName="FUNC013-USR-UTCID05")] public async Task Audio_size_boundaries_are_forwarded() { await ProfileBiometricTest.AssertFileLength("voice", 1); await ProfileBiometricTest.AssertFileLength("voice", 10_000); }
    [Fact(DisplayName="FUNC013-USR-UTCID06")] public async Task Voice_dependency_failure_is_unavailable() => await ProfileBiometricTest.AssertEnrollment("voice", 503, new InvalidOperationException("dependency"));
}

internal static class ProfileBiometricTest
{
    internal static (ProfileBiometricsController Controller, Mock<IProfileBiometricsService> Service, Guid UserId) Create(bool authenticated = true)
    {
        var service = new Mock<IProfileBiometricsService>(); var userId = Guid.NewGuid(); var context = new DefaultHttpContext();
        if (authenticated) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));
        return (new ProfileBiometricsController(service.Object, NullLogger<ProfileBiometricsController>.Instance) { ControllerContext = new ControllerContext { HttpContext = context } }, service, userId);
    }

    internal static async Task AssertEnrollment(string modality, int expectedStatus, Exception? error = null)
    {
        var (controller, service, userId) = Create(); var file = File(8); var serviceModality = modality == "face" ? ProfileBiometricsService.ModalityFace : ProfileBiometricsService.ModalityVoice;
        var setup = service.Setup(x => x.EnrollAsync(userId, serviceModality, file, It.IsAny<CancellationToken>()));
        if (error is null) setup.ReturnsAsync(new BiometricEnrollResponse { Modality = serviceModality, Dim = 128 }); else setup.ThrowsAsync(error);
        var result = modality == "face" ? await controller.EnrollFace(file, default) : await controller.EnrollVoice(file, default);
        Assert.Equal(expectedStatus, Status(result)); service.VerifyAll();
    }

    internal static async Task AssertMissingIdentity(bool voice)
    {
        var (controller, service, _) = Create(false); var file = File(1);
        var result = voice ? await controller.EnrollVoice(file, default) : await controller.EnrollFace(file, default);
        Assert.Equal(503, Status(result)); service.VerifyNoOtherCalls();
    }

    internal static async Task AssertFileLength(string modality, int length)
    {
        var (controller, service, userId) = Create(); var file = File(length); var serviceModality = modality == "face" ? ProfileBiometricsService.ModalityFace : ProfileBiometricsService.ModalityVoice;
        service.Setup(x => x.EnrollAsync(userId, serviceModality, It.Is<IFormFile>(f => f.Length == length), It.IsAny<CancellationToken>())).ReturnsAsync(new BiometricEnrollResponse { Modality = serviceModality });
        Assert.Equal(200, Status(modality == "face" ? await controller.EnrollFace(file, default) : await controller.EnrollVoice(file, default))); service.VerifyAll();
    }

    internal static async Task AssertStatus(bool face, bool voice)
    {
        var (controller, service, userId) = Create(); service.Setup(x => x.GetStatusAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(new BiometricStatusResponse { FaceEnrolled = face, VoiceEnrolled = voice });
        var response = Assert.IsType<ApiResponse<BiometricStatusResponse>>(Assert.IsType<OkObjectResult>(await controller.GetStatus(default)).Value);
        Assert.Equal(face, response.Data!.FaceEnrolled); Assert.Equal(voice, response.Data.VoiceEnrolled);
    }

    internal static int? Status(IActionResult result) => (result as ObjectResult)?.StatusCode;
    private static IFormFile File(int length) => new FormFile(new MemoryStream(new byte[length]), 0, length, "file", "sample.bin") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };
}
