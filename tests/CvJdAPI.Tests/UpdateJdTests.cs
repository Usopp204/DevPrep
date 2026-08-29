using CvJdAPI.Endpoints;
using CvJdAPI.Models;
using Xunit;

namespace CvJdAPI.Tests;

public class UpdateJdTests
{
    [Fact(DisplayName = "FUNC028-USR-UTCID01")] public async Task Existing_latest_jd_is_updated() { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var old = new JobDescription { JdId = Guid.NewGuid(), UserId = user, RawText = "old", Position = "Old", CreatedAt = DateTime.UtcNow }; db.JobDescriptions.Add(old); await db.SaveChangesAsync(); var before = DateTime.UtcNow; Assert.Equal(200, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync(user.ToString(), "new description", "Senior Dev", db))); Assert.Equal("new description", old.RawText); Assert.Equal("Senior Dev", old.Title); Assert.True(old.UpdatedAt >= before); }
    [Fact(DisplayName = "FUNC028-USR-UTCID02")] public async Task Invalid_identifier_format_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync("invalid-format", "text", null, db))); Assert.Empty(db.JobDescriptions); }
    [Fact(DisplayName = "FUNC028-USR-UTCID03")] public async Task Null_or_empty_required_input_is_rejected() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync(null, "text", null, db))); Assert.Equal(400, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync(Guid.NewGuid().ToString(), "", null, db))); }
    [Fact(DisplayName = "FUNC028-USR-UTCID04")] public async Task Missing_jd_returns_not_found() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync(Guid.NewGuid().ToString(), "text", null, db))); }
    [Fact(DisplayName = "FUNC028-USR-UTCID05")] public async Task Boundary_identifiers_are_valid_without_false_match() { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync(Guid.Empty.ToString(), "min", null, db))); Assert.Equal(404, ViewCurrentCvTests.Status(await JdEndpoints.UpdateJdAsync("ffffffff-ffff-ffff-ffff-ffffffffffff", "max", null, db))); }
    [Fact(DisplayName = "FUNC028-USR-UTCID06")] public async Task Dependency_failure_is_propagated_to_safe_error_middleware() { var db = ViewCurrentCvTests.CreateDb(); await db.DisposeAsync(); await Assert.ThrowsAsync<ObjectDisposedException>(() => JdEndpoints.UpdateJdAsync(Guid.NewGuid().ToString(), "text", null, db)); }
}
