namespace AuthAPI.Services.Interfaces;

/// <summary>
/// Verify Firebase ID token (chữ ký RS256 bằng khóa công khai của Google, kiểm tra issuer/audience/hết hạn)
/// rồi chuẩn hoá thành <see cref="ExternalUserInfo"/>. Không cần service account — chỉ cần Firebase project ID.
/// </summary>
public interface IFirebaseTokenValidator
{
    Task<ExternalUserInfo> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
