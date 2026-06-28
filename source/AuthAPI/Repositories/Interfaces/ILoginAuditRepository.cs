using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface ILoginAuditRepository
{
    Task AddAsync(LoginAudit audit);
}
