using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;

namespace NotificationAPI.Repositories;

public class EmailLogRepository : IEmailLogRepository
{
    private readonly NotificationDbContext _context;

    public EmailLogRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailLog log) =>
        await _context.EmailLogs.AddAsync(log);

    public Task<int> SaveChangesAsync() =>
        _context.SaveChangesAsync();
}
