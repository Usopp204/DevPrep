using NotificationAPI.Models;

namespace NotificationAPI.Repositories.Interfaces;

public interface IEmailLogRepository
{
    Task AddAsync(EmailLog log);

    Task<int> SaveChangesAsync();
}
