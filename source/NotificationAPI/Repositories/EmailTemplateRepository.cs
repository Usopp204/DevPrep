using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;
using NotificationAPI.Repositories.Interfaces;

namespace NotificationAPI.Repositories;

public class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly NotificationDbContext _context;

    public EmailTemplateRepository(NotificationDbContext context)
    {
        _context = context;
    }

    public Task<EmailTemplate?> GetActiveByCodeAsync(string code) =>
        _context.EmailTemplates.FirstOrDefaultAsync(t => t.Code == code && t.IsActive);

    public Task<bool> ExistsByCodeAsync(string code) =>
        _context.EmailTemplates.AnyAsync(t => t.Code == code);

    public Task<EmailTemplate?> GetByCodeAsync(string code) =>
        _context.EmailTemplates.FirstOrDefaultAsync(t => t.Code == code);

    public async Task AddAsync(EmailTemplate template) =>
        await _context.EmailTemplates.AddAsync(template);

    public Task<int> SaveChangesAsync() =>
        _context.SaveChangesAsync();
}
