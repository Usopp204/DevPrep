using NotificationAPI.Models;

namespace NotificationAPI.Repositories.Interfaces;

public interface IEmailTemplateRepository
{
    /// <summary>
    /// Lấy template đang bật (IsActive) theo mã (vd "VERIFY_EMAIL").
    /// </summary>
    Task<EmailTemplate?> GetActiveByCodeAsync(string code);

    Task<bool> ExistsByCodeAsync(string code);

    /// <summary>
    /// Lấy template theo mã bất kể IsActive (tracked) — phục vụ seeder nâng cấp nội dung template cũ.
    /// </summary>
    Task<EmailTemplate?> GetByCodeAsync(string code);

    Task AddAsync(EmailTemplate template);

    Task<int> SaveChangesAsync();
}
