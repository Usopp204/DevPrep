namespace AuthAPI.Services.Interfaces;

public interface IGoogleTokenValidator
{
    /// <summary>
    /// Xác thực Google ID token và trả về thông tin người dùng. Ném lỗi nếu token không hợp lệ.
    /// </summary>
    Task<ExternalUserInfo> ValidateAsync(string idToken);
}
