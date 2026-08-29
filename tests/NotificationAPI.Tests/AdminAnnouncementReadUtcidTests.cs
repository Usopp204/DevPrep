using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using Xunit;

namespace NotificationAPI.Tests;

public class AdminAnnouncementReadUtcidTests
{
    [Fact(DisplayName = "FUNC151-ADM-UTCID01")]
    public async Task Admin_list_returns_non_deleted_active_and_inactive_items()
    {
        await using var db = Db();
        db.Announcements.AddRange(Item("Active", true, false), Item("Inactive", false, false));
        await db.SaveChangesAsync();

        var items = await new AnnouncementRepository(db).GetAdminListAsync(false, 100);

        Assert.Equal(2, items.Count);
        Assert.Contains(items, x => x.Title == "Active" && x.IsActive);
        Assert.Contains(items, x => x.Title == "Inactive" && !x.IsActive);
    }

    [Fact(DisplayName = "FUNC151-ADM-UTCID02")]
    public async Task Deleted_filter_returns_only_soft_deleted_items()
    {
        await using var db = Db();
        db.Announcements.AddRange(Item("Visible", true, false), Item("Deleted", true, true));
        await db.SaveChangesAsync();

        var items = await new AnnouncementRepository(db).GetAdminListAsync(true, 100);

        Assert.Collection(items, item => { Assert.Equal("Deleted", item.Title); Assert.True(item.IsDeleted); });
    }

    [Fact(DisplayName = "FUNC151-ADM-UTCID03")]
    public async Task Admin_list_orders_newest_first()
    {
        await using var db = Db();
        var old = Item("Old", true, false, DateTime.UtcNow.AddDays(-1));
        var latest = Item("Latest", true, false, DateTime.UtcNow);
        db.Announcements.AddRange(old, latest); await db.SaveChangesAsync();

        Assert.Equal(new[] { latest.AnnouncementId, old.AnnouncementId },
            (await new AnnouncementRepository(db).GetAdminListAsync(false, 100)).Select(x => x.AnnouncementId));
    }

    [Fact(DisplayName = "FUNC151-ADM-UTCID04")]
    public async Task Admin_list_empty_store_returns_empty_collection()
    {
        await using var db = Db();
        Assert.Empty(await new AnnouncementRepository(db).GetAdminListAsync(false, 100));
    }

    [Fact(DisplayName = "FUNC151-ADM-UTCID05")]
    public async Task Admin_list_honors_one_item_boundary()
    {
        await using var db = Db();
        db.Announcements.AddRange(Item("First", true, false, DateTime.UtcNow), Item("Second", true, false, DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var item = Assert.Single(await new AnnouncementRepository(db).GetAdminListAsync(false, 1));
        Assert.Equal("First", item.Title);
    }

    [Fact(DisplayName = "FUNC151-ADM-UTCID06")]
    public async Task Admin_list_is_no_tracking_and_does_not_leak_unsaved_mutation()
    {
        await using var db = Db();
        db.Announcements.Add(Item("Original", true, false)); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var item = Assert.Single(await new AnnouncementRepository(db).GetAdminListAsync(false, 100));
        item.Title = "Mutated outside context";
        db.ChangeTracker.Clear();

        Assert.Equal("Original", Assert.Single(await new AnnouncementRepository(db).GetAdminListAsync(false, 100)).Title);
    }

    private static NotificationDbContext Db() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Announcement Item(string title, bool active, bool deleted, DateTime? created = null)
    {
        var time = created ?? DateTime.UtcNow;
        return new Announcement
        {
            AnnouncementId = Guid.NewGuid(), Title = title, Content = "Content", IsActive = active,
            IsDeleted = deleted, DeletedAt = deleted ? time : null, CreatedAt = time, UpdatedAt = time
        };
    }
}
