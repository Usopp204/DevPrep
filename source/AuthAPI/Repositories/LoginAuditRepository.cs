using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;

namespace AuthAPI.Repositories;

public class LoginAuditRepository : ILoginAuditRepository
{
    private readonly AuthDbContext _context;

    public LoginAuditRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(LoginAudit audit) =>
        await _context.LoginAudits.AddAsync(audit);
}
