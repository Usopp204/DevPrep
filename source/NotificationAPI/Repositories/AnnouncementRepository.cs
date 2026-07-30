using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;

namespace NotificationAPI.Repositories;

public class AnnouncementRepository : IAnnouncementRepository
{
    private readonly NotificationDbContext _context;

    public AnnouncementRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<List<Announcement>> GetActiveAsync(int maxCount) =>
        _context.Announcements
            .AsNoTracking()
            .Where(a => a.IsActive && !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxCount)
            .ToListAsync();

    public Task<List<Announcement>> GetAdminListAsync(bool onlyDeleted, int maxCount) =>
        _context.Announcements
            .AsNoTracking()
            .Where(a => onlyDeleted ? a.IsDeleted : !a.IsDeleted)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxCount)
            .ToListAsync();

    public Task<Announcement?> GetTrackedByIdAsync(Guid id) =>
        _context.Announcements.FirstOrDefaultAsync(a => a.AnnouncementId == id);

    public async Task AddAsync(Announcement announcement) =>
        await _context.Announcements.AddAsync(announcement);

    public Task<int> SaveChangesAsync() =>
        _context.SaveChangesAsync();
}
