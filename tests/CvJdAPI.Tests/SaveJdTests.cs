using System.Net;
using CvJdAPI.Endpoints;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CvJdAPI.Tests;

public class SaveJdTests
{
    [Fact(DisplayName = "FUNC027-USR-UTCID01")] public async Task Normal_mode_persists_jd_and_structured_json() { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var result = await JdEndpoints.SaveJdAsync(user.ToString(), "Build APIs", "Backend Engineer", "normal", db, Factory(), Config()); Assert.Equal(200, ViewCurrentCvTests.Status(result)); var jd = Assert.Single(db.JobDescriptions); Assert.Equal(user, jd.UserId); Assert.Equal("Backend Engineer", jd.Title); Assert.Contains("Build APIs", jd.ClarifiedJson); Assert.Contains("normal", jd.ClarifiedJson); }
    [Fact(DisplayName = "FUNC027-USR-UTCID02")] public async Task Invalid_identifier_format_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.SaveJdAsync("invalid-format", "text", null, "normal", db, Factory(), Config()))); Assert.Empty(db.JobDescriptions); }
    [Fact(DisplayName = "FUNC027-USR-UTCID03")] public async Task Null_or_empty_required_input_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.SaveJdAsync(null, "text", null, "normal", db, Factory(), Config()))); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.SaveJdAsync(Guid.NewGuid().ToString(), "", null, "normal", db, Factory(), Config()))); }
    [Fact(DisplayName = "FUNC027-USR-UTCID04")] public async Task Ai_rejection_persists_raw_jd_without_clarified_json() { await using var db = ViewCurrentCvTests.CreateDb(); var result = await JdEndpoints.SaveJdAsync(Guid.NewGuid().ToString(), "raw", "Dev", "ai", db, Factory(HttpStatusCode.UnprocessableEntity), Config()); Assert.Equal(200, ViewCurrentCvTests.Status(result)); Assert.Null(Assert.Single(db.JobDescriptions).ClarifiedJson); }
    [Fact(DisplayName = "FUNC027-USR-UTCID05")] public async Task Boundary_identifiers_are_accepted() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(200, ViewCurrentCvTests.Status(await JdEndpoints.SaveJdAsync(Guid.Empty.ToString(), "min", null, "normal", db, Factory(), Config()))); Assert.Equal(200, ViewCurrentCvTests.Status(await JdEndpoints.SaveJdAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", "max", null, "normal", db, Factory(), Config()))); Assert.Equal(2, db.JobDescriptions.Count()); }
    [Fact(DisplayName = "FUNC027-USR-UTCID06")] public async Task Missing_ai_configuration_raises_dependency_error() { await using var db = ViewCurrentCvTests.CreateDb(); await Assert.ThrowsAsync<InvalidOperationException>(() => JdEndpoints.SaveJdAsync(Guid.NewGuid().ToString(), "text", null, "ai", db, Factory(), new ConfigurationBuilder().Build())); Assert.Empty(db.JobDescriptions); }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI_SERVICE_URL"] = "http://ai" }).Build();
    private static IHttpClientFactory Factory(HttpStatusCode status = HttpStatusCode.OK) => new FactoryStub(new HttpClient(new Handler(status)));
    private sealed class FactoryStub(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"position\":\"Dev\"}") }); }
}
