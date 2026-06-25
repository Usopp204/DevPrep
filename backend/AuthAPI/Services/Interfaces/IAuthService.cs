using AuthAPI.DTOs;

namespace AuthAPI.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);

    Task<VerifyEmailOtpResponse> VerifyEmailOtpAsync(VerifyEmailOtpRequest request);

    Task<ResendEmailOtpResponse> ResendEmailOtpAsync(ResendEmailOtpRequest request);

    /// <summary>Đăng nhập thường (mọi role).</summary>
    Task<LoginResponse> LoginAsync(LoginRequest request);

    /// <summary>Đăng nhập khu vực quản trị (chỉ chấp nhận Admin/Manager).</summary>
    Task<LoginResponse> AdminLoginAsync(LoginRequest request);

    /// <summary>Đăng nhập bằng Google (ID token từ frontend).</summary>
    Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request);

    /// <summary>Đăng nhập bằng GitHub (authorization code từ frontend).</summary>
    Task<LoginResponse> GitHubLoginAsync(GitHubLoginRequest request);

    /// <summary>
    /// Đăng nhập SSO theo Firebase (dùng chung cho Google lẫn GitHub). FE gửi Firebase ID token;
    /// backend verify token (chữ ký Google + issuer/audience/hết hạn) rồi rút định danh từ claim đã xác minh.
    /// </summary>
    Task<LoginResponse> FirebaseLoginAsync(FirebaseLoginRequest request);

    /// <summary>Yêu cầu đặt lại mật khẩu: gửi link reset qua email (nếu tài khoản tồn tại).</summary>
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>Đặt lại mật khẩu bằng token nhận từ email.</summary>
    Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>Đổi mật khẩu cho người dùng đang đăng nhập (cần mật khẩu hiện tại).</summary>
    Task<ChangePasswordResponse> ChangePasswordAsync(Guid accountId, ChangePasswordRequest request);

    /// <summary>Cấp access token mới bằng refresh token (kèm xoay vòng refresh token).</summary>
    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>Đăng xuất: thu hồi refresh token.</summary>
    Task<LogoutResponse> LogoutAsync(LogoutRequest request);
}
