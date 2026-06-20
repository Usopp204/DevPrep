using AuthAPI.Services.Interfaces;

namespace AuthAPI.Services;

/// <summary>
/// Triển khai băm mật khẩu bằng BCrypt. Hash đã tự chứa salt nên không cần lưu salt riêng.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor càng cao càng an toàn nhưng chậm hơn. 12 là mức cân bằng hợp lý.
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hashedPassword) =>
        BCrypt.Net.BCrypt.Verify(password, hashedPassword);
}
