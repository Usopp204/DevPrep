using System.Security.Claims;
using EvaluationAPI.Clients;
using EvaluationAPI.Controllers;
using EvaluationAPI.DTOs;
using EvaluationAPI.DTOs.Evaluation;
using EvaluationAPI.Services;
using EvaluationAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EvaluationAPI.Tests;

public class EvaluationControllerTests
{
    [Fact]
    public async Task EvaluateAnswer_returns_forbidden_when_session_is_not_owned()
    {
        var (controller, service, client) = Create(); var session = Guid.NewGuid();
        client.Setup(x => x.OwnsSessionAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = Assert.IsType<ObjectResult>(await controller.EvaluateAnswer(session, Guid.NewGuid(), new EvaluateAnswerRequest(), default));
        Assert.Equal(403, result.StatusCode); service.Verify(x => x.EvaluateAnswerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<EvaluateAnswerRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateAnswer_returns_service_score_when_owned()
    {
        var (controller, service, client) = Create(); var session = Guid.NewGuid(); var answer = Guid.NewGuid(); var request = new EvaluateAnswerRequest { UserAnswer = "answer" }; var expected = new EvaluateAnswerResponse { SessionId = session, SessionQuestionId = answer, Score = 8.5m };
        client.Setup(x => x.OwnsSessionAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true); service.Setup(x => x.EvaluateAnswerAsync(session, answer, request, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.EvaluateAnswer(session, answer, request, default));
        Assert.Equal(8.5m, Assert.IsType<ApiResponse<EvaluateAnswerResponse>>(ok.Value).Data!.Score);
    }

    public static IEnumerable<object[]> EvaluationErrors()
    {
        yield return new object[] { new InterviewApiUnauthorizedException(), 401 };
        yield return new object[] { new InterviewApiForbiddenException(), 403 };
        yield return new object[] { new InterviewApiSessionNotFoundException(), 403 };
        yield return new object[] { new InterviewApiTimeoutException(), 503 };
        yield return new object[] { new InterviewApiDependencyException(), 503 };
        yield return new object[] { new EvaluationDependencyException(), 503 };
        yield return new object[] { new EvaluationPersistenceException(new Exception()), 503 };
        yield return new object[] { new EvaluationRequestException(), 400 };
        yield return new object[] { new ArgumentException(), 400 };
        yield return new object[] { new InvalidOperationException(), 400 };
        yield return new object[] { new Exception(), 503 };
    }

    [Theory]
    [MemberData(nameof(EvaluationErrors))]
    public async Task EvaluateAnswer_maps_typed_failures(Exception error, int status)
    {
        var (controller, service, client) = Create(); var session = Guid.NewGuid(); var answer = Guid.NewGuid(); var request = new EvaluateAnswerRequest();
        client.Setup(x => x.OwnsSessionAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true); service.Setup(x => x.EvaluateAnswerAsync(session, answer, request, It.IsAny<CancellationToken>())).ThrowsAsync(error);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await controller.EvaluateAnswer(session, answer, request, default)).StatusCode);
    }

    [Fact]
    public async Task Performance_requires_authenticated_user_claim()
    {
        var (controller, service, _) = Create(authenticated: false);
        Assert.IsType<UnauthorizedObjectResult>(await controller.GetPerformance(Guid.NewGuid(), default));
        service.Verify(x => x.GetPerformanceReportAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Performance_returns_report_and_forwards_bearer_token()
    {
        var (controller, service, _) = Create(); var session = Guid.NewGuid(); controller.Request.Headers.Authorization = "Bearer token"; var expected = new PerformanceReportResponse { SessionId = session, State = "Scored", OverallScore = 9 };
        service.Setup(x => x.GetPerformanceReportAsync(session, "Bearer token", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetPerformance(session, default)); Assert.Equal(9, Assert.IsType<ApiResponse<PerformanceReportResponse>>(ok.Value).Data!.OverallScore);
    }

    public static IEnumerable<object[]> PerformanceErrors()
    {
        yield return new object[] { new InterviewApiUnauthorizedException(), 401 };
        yield return new object[] { new InterviewApiForbiddenException(), 403 };
        yield return new object[] { new InterviewApiSessionNotFoundException(), 404 };
        yield return new object[] { new SessionNotEligibleForPerformanceReportException("Active"), 409 };
        yield return new object[] { new InterviewApiTimeoutException(), 503 };
        yield return new object[] { new InterviewApiDependencyException(), 503 };
        yield return new object[] { new Exception(), 503 };
    }

    [Theory]
    [MemberData(nameof(PerformanceErrors))]
    public async Task Performance_maps_typed_failures(Exception error, int status)
    {
        var (controller, service, _) = Create(); var session = Guid.NewGuid(); service.Setup(x => x.GetPerformanceReportAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(error);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(await controller.GetPerformance(session, default)).StatusCode);
    }

    [Fact]
    public async Task EnsureReport_returns_persisted_report()
    {
        var (controller, service, _) = Create(); var session = Guid.NewGuid(); var expected = new PerformanceReportResponse { SessionId = session, HasPersistedReport = true }; service.Setup(x => x.EnsurePerformanceReportAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.EnsureReport(session, default)); Assert.True(Assert.IsType<ApiResponse<PerformanceReportResponse>>(ok.Value).Data!.HasPersistedReport);
    }

    [Fact]
    public async Task EnsureReport_maps_not_ready_to_conflict()
    {
        var (controller, service, _) = Create(); var session = Guid.NewGuid(); service.Setup(x => x.EnsurePerformanceReportAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new AggregateReportNotReadyException("Grading"));
        Assert.IsType<ConflictObjectResult>(await controller.EnsureReport(session, default));
    }

    [Fact]
    public async Task AnswerEvaluations_returns_forbidden_when_not_owned()
    {
        var (controller, service, client) = Create(); var session = Guid.NewGuid(); client.Setup(x => x.OwnsSessionAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Assert.Equal(403, Assert.IsType<ObjectResult>(await controller.GetAnswerEvaluations(session, default)).StatusCode); service.Verify(x => x.GetAnswerEvaluationsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnswerEvaluations_returns_service_items()
    {
        var (controller, service, client) = Create(); var session = Guid.NewGuid(); var expected = new List<EvaluateAnswerResponse> { new() { Score = 7 } }; client.Setup(x => x.OwnsSessionAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true); service.Setup(x => x.GetAnswerEvaluationsAsync(session, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var ok = Assert.IsType<OkObjectResult>(await controller.GetAnswerEvaluations(session, default)); Assert.Single(Assert.IsType<ApiResponse<List<EvaluateAnswerResponse>>>(ok.Value).Data!);
    }

    private static (EvaluationController Controller, Mock<IEvaluationService> Service, Mock<IInterviewApiClient> Client) Create(bool authenticated = true)
    {
        var service = new Mock<IEvaluationService>(); var client = new Mock<IInterviewApiClient>(); var context = new DefaultHttpContext();
        if (authenticated) context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }));
        var accessor = new Mock<IHttpContextAccessor>(); accessor.SetupGet(x => x.HttpContext).Returns(context);
        var controller = new EvaluationController(service.Object, client.Object, accessor.Object) { ControllerContext = new ControllerContext { HttpContext = context } };
        return (controller, service, client);
    }
}
