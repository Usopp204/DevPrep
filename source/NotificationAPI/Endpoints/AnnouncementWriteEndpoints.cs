using NotificationAPI.Helpers;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;

namespace NotificationAPI.Endpoints;

public static class AnnouncementWriteEndpoints
{
    public static void MapAnnouncementWriteEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/announcements", CreateAsync).RequireAuthorization("AnnouncementAdmin");
        app.MapPut("/admin/announcements/{id:guid}", UpdateAsync).RequireAuthorization("AnnouncementAdmin");
        app.MapDelete("/admin/announcements/{id:guid}", DeleteAsync).RequireAuthorization("AnnouncementAdmin");
    }

    public static async Task<IResult> CreateAsync(AnnouncementRequest req, IAnnouncementRepository repo)
    {
        var error = Validate(req);
        if (error is not null) return Results.BadRequest(new { Message = error });
        var now = VietnamTime.Now;
        var announcement = new Announcement
        {
            AnnouncementId = Guid.NewGuid(), Title = req.Title.Trim(), Content = req.Content.Trim(),
            IsActive = req.IsActive, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        };
        await repo.AddAsync(announcement);
        await repo.SaveChangesAsync();
        return Results.Ok(new { announcement.AnnouncementId });
    }

    public static async Task<IResult> UpdateAsync(Guid id, AnnouncementRequest req, IAnnouncementRepository repo)
    {
        var error = Validate(req);
        if (error is not null) return Results.BadRequest(new { Message = error });
        var announcement = await repo.GetTrackedByIdAsync(id);
        if (announcement is null || announcement.IsDeleted)
            return Results.NotFound(new { Message = "Announcement not found." });
        announcement.Title = req.Title.Trim();
        announcement.Content = req.Content.Trim();
        announcement.IsActive = req.IsActive;
        announcement.UpdatedAt = VietnamTime.Now;
        await repo.SaveChangesAsync();
        return Results.Ok(new { announcement.AnnouncementId });
    }

    public static async Task<IResult> DeleteAsync(Guid id, IAnnouncementRepository repo)
    {
        var announcement = await repo.GetTrackedByIdAsync(id);
        if (announcement is null) return Results.NotFound(new { Message = "Announcement not found." });
        if (!announcement.IsDeleted)
        {
            var now = VietnamTime.Now;
            announcement.IsDeleted = true;
            announcement.DeletedAt = now;
            announcement.UpdatedAt = now;
            await repo.SaveChangesAsync();
        }
        return Results.NoContent();
    }

    private static string? Validate(AnnouncementRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return "Title is required.";
        if (req.Title.Trim().Length > 200) return "Title must not exceed 200 characters.";
        if (string.IsNullOrWhiteSpace(req.Content)) return "Content is required.";
        if (req.Content.Trim().Length > 2000) return "Content must not exceed 2000 characters.";
        return null;
    }
}

public record AnnouncementRequest(string Title, string Content, bool IsActive = true);
