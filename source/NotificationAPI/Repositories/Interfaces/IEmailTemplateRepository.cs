using NotificationAPI.Models;

namespace NotificationAPI.Repositories.Interfaces;

public interface IEmailTemplateRepository
{
    /// <summary>
    /// Lấy template đang bật (IsActive) theo mã (vd "VERIFY_EMAIL").
    /// </summary>
    Task<EmailTemplate?> GetActiveByCodeAsync(string code);

    Task<bool> ExistsByCodeAsync(string code);

    Task AddAsync(EmailTemplate template);

    Task<int> SaveChangesAsync();
}
