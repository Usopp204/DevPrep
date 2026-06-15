namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Băm và kiểm tra mật khẩu. Tách thành interface để dễ thay thuật toán sau này.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hashedPassword);
}
