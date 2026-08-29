using System.Net;
using CvJdAPI.Endpoints;
using CvJdAPI.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CvJdAPI.Tests;

public class CalculateMatchTests
{
    [Fact(DisplayName = "FUNC030-USR-UTCID01")]
    public async Task Success_persists_score_keywords_and_suggestions()
    {
        await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid();
        db.Cvs.Add(new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, ParsedJson = "{\"skills\":[\"C#\"]}" });
        db.JobDescriptions.Add(new JobDescription { JdId = Guid.NewGuid(), UserId = user, ClarifiedJson = "{\"required\":[\"C#\"]}" }); await db.SaveChangesAsync();
        var json = "{\"score\":87,\"matched_skills\":[\"C#\"],\"missing_skills\":[\"SQL\"],\"suggestions\":[\"Learn SQL\"]}";
        var result = await MatchEndpoints.CalculateMatchAsync(user.ToString(), db, Factory(HttpStatusCode.OK, json), Config());
        Assert.Equal(200, ViewCurrentCvTests.Status(result)); var saved = Assert.Single(db.MatchingScores); Assert.Equal(87m, saved.Score); Assert.Equal("[\"C#\"]", saved.MatchedKeywords); Assert.Equal("[\"SQL\"]", saved.MissingKeywords); Assert.Contains("Learn SQL", saved.SuggestionText);
    }
    [Fact(DisplayName = "FUNC030-USR-UTCID02")] public async Task Invalid_identifier_format_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync("invalid-format", db, Factory(), Config()))); Assert.Empty(db.MatchingScores); }
    [Fact(DisplayName = "FUNC030-USR-UTCID03")] public async Task Null_or_empty_identifier_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync(null, db, Factory(), Config()))); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync("", db, Factory(), Config()))); }
    [Fact(DisplayName = "FUNC030-USR-UTCID04")] public async Task Missing_parsed_cv_or_jd_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); db.Cvs.Add(new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, ParsedJson = "{}" }); await db.SaveChangesAsync(); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync(user.ToString(), db, Factory(), Config()))); }
    [Fact(DisplayName = "FUNC030-USR-UTCID05")] public async Task Boundary_identifiers_are_valid_but_do_not_false_match() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync(Guid.Empty.ToString(), db, Factory(), Config()))); Assert.Equal(400, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", db, Factory(), Config()))); }
    [Fact(DisplayName = "FUNC030-USR-UTCID06")] public async Task Ai_failure_returns_bad_gateway_without_persisting_score() { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); db.Cvs.Add(new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, ParsedJson = "{}" }); db.JobDescriptions.Add(new JobDescription { JdId = Guid.NewGuid(), UserId = user, ClarifiedJson = "{}" }); await db.SaveChangesAsync(); Assert.Equal(502, ViewCurrentCvTests.Status(await MatchEndpoints.CalculateMatchAsync(user.ToString(), db, Factory(HttpStatusCode.InternalServerError), Config()))); Assert.Empty(db.MatchingScores); }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI_SERVICE_URL"] = "http://ai" }).Build();
    private static IHttpClientFactory Factory(HttpStatusCode status = HttpStatusCode.OK, string body = "{}") => new StubFactory(new HttpClient(new StubHandler(status, body)));
    private sealed class StubFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
}
