using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using Xunit;

namespace NotificationAPI.Tests;

public class ViewAnnouncementUtcidTests
{
    [Fact(DisplayName="FUNC019-USR-UTCID01")] public async Task Active_announcement_is_visible() { await using var db = Db(); db.Announcements.Add(Item(true, false, DateTime.UtcNow)); await db.SaveChangesAsync(); Assert.Single(await new AnnouncementRepository(db).GetActiveAsync(10)); }
    [Fact(DisplayName="FUNC019-USR-UTCID02")] public async Task Empty_store_returns_empty_list() { await using var db = Db(); Assert.Empty(await new AnnouncementRepository(db).GetActiveAsync(10)); }
    [Fact(DisplayName="FUNC019-USR-UTCID03")] public async Task Inactive_announcement_is_hidden() { await using var db = Db(); db.Announcements.Add(Item(false, false, DateTime.UtcNow)); await db.SaveChangesAsync(); Assert.Empty(await new AnnouncementRepository(db).GetActiveAsync(10)); }
    [Fact(DisplayName="FUNC019-USR-UTCID04")] public async Task Deleted_announcement_is_hidden() { await using var db = Db(); db.Announcements.Add(Item(true, true, DateTime.UtcNow)); await db.SaveChangesAsync(); Assert.Empty(await new AnnouncementRepository(db).GetActiveAsync(10)); }
    [Fact(DisplayName="FUNC019-USR-UTCID05")] public async Task Results_are_newest_first() { await using var db = Db(); var old = Item(true, false, DateTime.UtcNow.AddDays(-1)); var recent = Item(true, false, DateTime.UtcNow); db.Announcements.AddRange(old, recent); await db.SaveChangesAsync(); Assert.Equal(new[] { recent.AnnouncementId, old.AnnouncementId }, (await new AnnouncementRepository(db).GetActiveAsync(10)).Select(x => x.AnnouncementId)); }
    [Fact(DisplayName="FUNC019-USR-UTCID06")] public async Task Maximum_count_is_enforced() { await using var db = Db(); db.Announcements.AddRange(Enumerable.Range(0, 5).Select(i => Item(true, false, DateTime.UtcNow.AddMinutes(-i)))); await db.SaveChangesAsync(); Assert.Equal(2, (await new AnnouncementRepository(db).GetActiveAsync(2)).Count); }

    private static NotificationDbContext Db() => new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Announcement Item(bool active, bool deleted, DateTime created) => new() { AnnouncementId = Guid.NewGuid(), Title = "Announcement", Content = "Content", IsActive = active, IsDeleted = deleted, CreatedAt = created, UpdatedAt = created };
}
