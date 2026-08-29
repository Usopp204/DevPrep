using CvJdAPI.Endpoints;
using CvJdAPI.Models;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CvJdAPI.Tests;

public class DeleteCvTests
{
    [Fact(DisplayName = "FUNC023-USR-UTCID01")] public async Task Success_deletes_cv_matches_and_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(dir); var file = Path.Combine(dir, "cv.pdf"); await File.WriteAllTextAsync(file, "cv");
        try { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var cv = new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, FileUrl = "/uploads/cvs/cv.pdf", UploadedAt = DateTime.UtcNow }; db.Cvs.Add(cv); db.MatchingScores.Add(new MatchingScore { MatchingScoreId = Guid.NewGuid(), CvId = cv.CvId, JdId = Guid.NewGuid(), UserId = user }); await db.SaveChangesAsync(); Assert.Equal(200, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync(user.ToString(), db, Config(dir)))); Assert.Empty(db.Cvs); Assert.Empty(db.MatchingScores); Assert.False(File.Exists(file)); }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Fact(DisplayName = "FUNC023-USR-UTCID02")] public async Task Invalid_format_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync("invalid-format", db, Config()))); }
    [Fact(DisplayName = "FUNC023-USR-UTCID03")] public async Task Null_or_empty_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync(null, db, Config()))); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync("", db, Config()))); }
    [Fact(DisplayName = "FUNC023-USR-UTCID04")] public async Task Missing_cv_returns_not_found() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync(Guid.NewGuid().ToString(), db, Config()))); }
    [Fact(DisplayName = "FUNC023-USR-UTCID05")] public async Task Boundary_ids_do_not_false_match() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync(Guid.Empty.ToString(), db, Config()))); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.DeleteMyCvAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", db, Config()))); }
    [Fact(DisplayName = "FUNC023-USR-UTCID06")] public async Task Dependency_failure_is_propagated() { var db = ViewCurrentCvTests.CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => CvEndpoints.DeleteMyCvAsync(Guid.NewGuid().ToString(), db, Config())); }
    private static IConfiguration Config(string? path = null) => new ConfigurationBuilder().AddInMemoryCollection(path is null ? null : new Dictionary<string, string?> { ["CV_UPLOADS_PATH"] = path }).Build();
}

public class DeleteJdTests
{
    [Fact(DisplayName = "FUNC029-USR-UTCID01")] public async Task Success_deletes_latest_jd_and_matches() { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var jd = new JobDescription { JdId = Guid.NewGuid(), UserId = user, CreatedAt = DateTime.UtcNow }; db.JobDescriptions.Add(jd); db.MatchingScores.Add(new MatchingScore { MatchingScoreId = Guid.NewGuid(), CvId = Guid.NewGuid(), JdId = jd.JdId, UserId = user }); await db.SaveChangesAsync(); Assert.Equal(200, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync(user.ToString(), db))); Assert.Empty(db.JobDescriptions); Assert.Empty(db.MatchingScores); }
    [Fact(DisplayName = "FUNC029-USR-UTCID02")] public async Task Invalid_format_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync("invalid-format", db))); }
    [Fact(DisplayName = "FUNC029-USR-UTCID03")] public async Task Null_or_empty_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync(null, db))); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync("", db))); }
    [Fact(DisplayName = "FUNC029-USR-UTCID04")] public async Task Missing_jd_returns_not_found() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync(Guid.NewGuid().ToString(), db))); }
    [Fact(DisplayName = "FUNC029-USR-UTCID05")] public async Task Boundary_ids_do_not_false_match() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync(Guid.Empty.ToString(), db))); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.DeleteMyJdAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", db))); }
    [Fact(DisplayName = "FUNC029-USR-UTCID06")] public async Task Dependency_failure_is_propagated() { var db = ViewCurrentCvTests.CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => JdEndpoints.DeleteMyJdAsync(Guid.NewGuid().ToString(), db)); }
}
