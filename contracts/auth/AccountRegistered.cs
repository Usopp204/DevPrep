namespace DevPrep.Contracts;

/// <summary>
/// Sự kiện AuthAPI phát ra khi một tài khoản mới được tạo (đăng ký thường hoặc SSO).
/// UserAPI lắng nghe để tạo hồ sơ (UserProfiles) tương ứng cho tài khoản đó.
/// </summary>
public record AccountRegistered
{
    public Guid AccountId { get; init; }

    public string Email { get; init; } = null!;

    /// <summary>Họ tên nhập lúc đăng ký (hoặc tên từ nhà cung cấp SSO). Có thể null.</summary>
    public string? FullName { get; init; }
}
