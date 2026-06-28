using AuthAPI.Models;

namespace AuthAPI.Repositories.Interfaces;

public interface ISsoIdentityRepository
{
    /// <summary>
    /// Tìm liên kết SSO theo nhà cung cấp và ID người dùng bên nhà cung cấp.
    /// </summary>
    Task<SsoIdentity?> GetByProviderAsync(string provider, string providerUserId);

    Task AddAsync(SsoIdentity identity);
}
