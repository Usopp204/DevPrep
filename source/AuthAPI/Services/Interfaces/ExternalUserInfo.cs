namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Thông tin người dùng đã chuẩn hoá lấy từ nhà cung cấp SSO (Google/GitHub).
/// </summary>
public record ExternalUserInfo(
    string Provider,
    string ProviderUserId,
    string? Email,
    bool EmailVerified,
    string? Name);
