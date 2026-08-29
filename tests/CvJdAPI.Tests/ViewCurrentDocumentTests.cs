using CvJdAPI.Data;
using CvJdAPI.Endpoints;
using CvJdAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CvJdAPI.Tests;

public class ViewCurrentCvTests
{
    [Fact(DisplayName = "FUNC020-USR-UTCID01")] public async Task FUNC020_USR_UTCID01_valid_owner_returns_current_cv()
    {
        await using var db = CreateDb(); var user = Guid.NewGuid(); var expected = new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, UploadedAt = DateTime.UtcNow }; db.Cvs.Add(expected); await db.SaveChangesAsync();
        var result = await CvEndpoints.GetMyCvAsync(user.ToString(), db); Assert.Equal(200, Status(result)); Assert.Same(expected, Value(result));
    }
    [Fact(DisplayName = "FUNC020-USR-UTCID02")] public async Task FUNC020_USR_UTCID02_invalid_format_returns_bad_request()
    { await using var db = CreateDb(); Assert.Equal(400, Status(await CvEndpoints.GetMyCvAsync("invalid-format", db))); }
    [Fact(DisplayName = "FUNC020-USR-UTCID03")] public async Task FUNC020_USR_UTCID03_empty_required_input_returns_bad_request()
    { await using var db = CreateDb(); Assert.Equal(400, Status(await CvEndpoints.GetMyCvAsync(string.Empty, db))); Assert.Equal(400, Status(await CvEndpoints.GetMyCvAsync(null, db))); }
    [Fact(DisplayName = "FUNC020-USR-UTCID04")] public async Task FUNC020_USR_UTCID04_unknown_owner_returns_not_found()
    { await using var db = CreateDb(); Assert.Equal(404, Status(await CvEndpoints.GetMyCvAsync(Guid.NewGuid().ToString(), db))); }
    [Fact(DisplayName = "FUNC020-USR-UTCID05")] public async Task FUNC020_USR_UTCID05_guid_boundaries_are_accepted_and_do_not_match()
    { await using var db = CreateDb(); Assert.Equal(404, Status(await CvEndpoints.GetMyCvAsync(Guid.Empty.ToString(), db))); Assert.Equal(404, Status(await CvEndpoints.GetMyCvAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", db))); }
    [Fact(DisplayName = "FUNC020-USR-UTCID06")] public async Task FUNC020_USR_UTCID06_dependency_failure_is_propagated_to_safe_error_middleware()
    { var db = CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => CvEndpoints.GetMyCvAsync(Guid.NewGuid().ToString(), db)); }

    internal static DocumentDbContext CreateDb() => new(new DbContextOptionsBuilder<DocumentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    internal static int? Status(IResult result) => (result as IStatusCodeHttpResult)?.StatusCode;
    internal static object? Value(IResult result) => (result as IValueHttpResult)?.Value;
}

public class ViewCurrentJdTests
{
    [Fact(DisplayName = "FUNC026-USR-UTCID01")] public async Task FUNC026_USR_UTCID01_valid_owner_returns_latest_jd()
    {
        await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var older = new JobDescription { JdId = Guid.NewGuid(), UserId = user, CreatedAt = DateTime.UtcNow.AddDays(-1) }; var expected = new JobDescription { JdId = Guid.NewGuid(), UserId = user, CreatedAt = DateTime.UtcNow }; db.JobDescriptions.AddRange(older, expected); await db.SaveChangesAsync();
        var result = await JdEndpoints.GetMyJdAsync(user.ToString(), db); Assert.Equal(200, ViewCurrentCvTests.Status(result)); Assert.Same(expected, ViewCurrentCvTests.Value(result));
    }
    [Fact(DisplayName = "FUNC026-USR-UTCID02")] public async Task FUNC026_USR_UTCID02_invalid_format_returns_bad_request()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync("invalid-format", db))); }
    [Fact(DisplayName = "FUNC026-USR-UTCID03")] public async Task FUNC026_USR_UTCID03_empty_required_input_returns_bad_request()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync(string.Empty, db))); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync(null, db))); }
    [Fact(DisplayName = "FUNC026-USR-UTCID04")] public async Task FUNC026_USR_UTCID04_unknown_owner_returns_not_found()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync(Guid.NewGuid().ToString(), db))); }
    [Fact(DisplayName = "FUNC026-USR-UTCID05")] public async Task FUNC026_USR_UTCID05_guid_boundaries_are_accepted_and_do_not_match()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync(Guid.Empty.ToString(), db))); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.GetMyJdAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", db))); }
    [Fact(DisplayName = "FUNC026-USR-UTCID06")] public async Task FUNC026_USR_UTCID06_dependency_failure_is_propagated_to_safe_error_middleware()
    { var db = ViewCurrentCvTests.CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => JdEndpoints.GetMyJdAsync(Guid.NewGuid().ToString(), db)); }
}

public class UpdateCvFileTests
{
    [Fact(DisplayName = "FUNC022-USR-UTCID01")]
    public async Task FUNC022_USR_UTCID01_existing_current_cv_is_updated()
    {
        await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var cv = new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, ParsedJson = "old", UploadedAt = DateTime.UtcNow }; db.Cvs.Add(cv); await db.SaveChangesAsync();
        var before = DateTime.UtcNow; var result = await CvEndpoints.UpdateCvJsonAsync(user.ToString(), "{\"skill\":\"dotnet\"}", db);
        Assert.Equal(200, ViewCurrentCvTests.Status(result)); Assert.Equal("{\"skill\":\"dotnet\"}", cv.ParsedJson); Assert.True(cv.UpdatedAt >= before);
    }

    [Fact(DisplayName = "FUNC022-USR-UTCID02")]
    public async Task FUNC022_USR_UTCID02_invalid_identifier_format_is_rejected()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync("invalid-format", "{}", db))); Assert.Empty(db.Cvs); }

    [Fact(DisplayName = "FUNC022-USR-UTCID03")]
    public async Task FUNC022_USR_UTCID03_null_or_empty_required_input_is_rejected()
    {
        await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid().ToString();
        Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync(null, "{}", db)));
        Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync(string.Empty, "{}", db)));
        Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync(user, null, db)));
    }

    [Fact(DisplayName = "FUNC022-USR-UTCID04")]
    public async Task FUNC022_USR_UTCID04_missing_cv_returns_not_found()
    { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync(Guid.NewGuid().ToString(), "{}", db))); }

    [Fact(DisplayName = "FUNC022-USR-UTCID05")]
    public async Task FUNC022_USR_UTCID05_identifier_boundaries_are_accepted_without_false_match()
    {
        await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync(Guid.Empty.ToString(), "{}", db))); Assert.Equal(404, ViewCurrentCvTests.Status(await CvEndpoints.UpdateCvJsonAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", "{}", db)));
    }

    [Fact(DisplayName = "FUNC022-USR-UTCID06")]
    public async Task FUNC022_USR_UTCID06_dependency_failure_is_propagated_to_safe_error_middleware()
    { var db = ViewCurrentCvTests.CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => CvEndpoints.UpdateCvJsonAsync(Guid.NewGuid().ToString(), "{}", db)); }
}
