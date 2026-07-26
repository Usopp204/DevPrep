using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthAPI.DTOs;
using AuthAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng ký tài khoản mới. Tài khoản được tạo ở trạng thái "Pending"
    /// và cần xác thực email bằng OTP trước khi sử dụng.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        var response = ApiResponse<RegisterResponse>.Ok(
            result,
            "Registration successful. Please check your email to verify your account.");

        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Xác thực email bằng mã OTP. Khi thành công, tài khoản chuyển sang trạng thái "Active".
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse<VerifyEmailOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailOtpRequest request)
    {
        var result = await _authService.VerifyEmailOtpAsync(request);
        return Ok(ApiResponse<VerifyEmailOtpResponse>.Ok(result, "Email verified successfully."));
    }

    /// <summary>
    /// Xác thực email bằng token trong link (người dùng bấm nút xác nhận ở trang ConfirmEmail).
    /// Không cần email/OTP — token dài trong link là đủ định danh lượt đăng ký.
    /// </summary>
    [HttpPost("verify-email-link")]
    [ProducesResponseType(typeof(ApiResponse<VerifyEmailOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmailLink([FromBody] VerifyEmailLinkRequest request)
    {
        var result = await _authService.VerifyEmailByLinkAsync(request);
        return Ok(ApiResponse<VerifyEmailOtpResponse>.Ok(result, "Email verified successfully."));
    }

    /// <summary>
    /// Gửi lại mã OTP xác thực email. Các OTP cũ chưa dùng sẽ bị vô hiệu hoá.
    /// </summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ApiResponse<ResendEmailOtpResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendEmailOtpRequest request)
    {
        var result = await _authService.ResendEmailOtpAsync(request);
        return Ok(ApiResponse<ResendEmailOtpResponse>.Ok(result, "A new verification OTP has been sent."));
    }

    /// <summary>
    /// Đăng nhập thường (mọi role). Trả về JWT access token + refresh token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>
    /// Đăng nhập khu vực quản trị. Chỉ chấp nhận tài khoản Admin/Manager.
    /// </summary>
    [HttpPost("admin/login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequest request)
    {
        var result = await _authService.AdminLoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Admin login successful."));
    }

    /// <summary>
    /// Đăng nhập/đăng ký bằng Google. Frontend gửi ID token lấy từ Google Sign-In.
    /// </summary>
    [HttpPost("sso/google")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GoogleSso([FromBody] GoogleLoginRequest request)
    {
        var result = await _authService.GoogleLoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>
    /// Đăng nhập/đăng ký bằng GitHub. Frontend gửi authorization code lấy từ GitHub.
    /// </summary>
    [HttpPost("sso/github")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GitHubSso([FromBody] GitHubLoginRequest request)
    {
        var result = await _authService.GitHubLoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>
    /// Đăng nhập/đăng ký bằng Firebase (dùng chung cho Google lẫn GitHub). FE gửi Firebase ID token
    /// (lấy từ user.getIdToken()). Backend VERIFY token (chữ ký RS256 bằng khóa công khai của Google,
    /// kiểm issuer/audience/hết hạn) rồi tự rút uid/email/name/provider từ claim đã xác minh —
    /// KHÔNG tin thông tin định danh do client tự gửi.
    /// </summary>
    [HttpPost("sso/firebase")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> FirebaseSso([FromBody] FirebaseLoginRequest request)
    {
        var result = await _authService.FirebaseLoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful."));
    }

    /// <summary>
    /// Quên mật khẩu: gửi link đặt lại mật khẩu qua email. Luôn trả về cùng một
    /// thông báo dù email có tồn tại hay không (chống dò tài khoản).
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.ForgotPasswordAsync(request);
        return Ok(ApiResponse<ForgotPasswordResponse>.Ok(result, result.Message));
    }

    /// <summary>
    /// Đặt lại mật khẩu bằng token nhận từ email.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<ResetPasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request);
        return Ok(ApiResponse<ResetPasswordResponse>.Ok(result, result.Message));
    }

    /// <summary>
    /// Đổi mật khẩu cho người dùng đang đăng nhập (cần access token + mật khẩu hiện tại).
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<ChangePasswordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(accountIdValue, out var accountId))
        {
            return Unauthorized();
        }

        var result = await _authService.ChangePasswordAsync(accountId, request);
        return Ok(ApiResponse<ChangePasswordResponse>.Ok(result, result.Message));
    }

    /// <summary>
    /// Cấp access token mới bằng refresh token (xoay vòng refresh token). Không cần đăng nhập:
    /// chính refresh token đóng vai trò chứng thực.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Token refreshed successfully."));
    }

    /// <summary>
    /// Trạng thái + role hiện tại của tài khoản đang đăng nhập. WebClient poll endpoint này
    /// (cache ngắn) để tự đăng xuất ngay khi tài khoản bị block giữa phiên.
    /// </summary>
    [Authorize]
    [HttpGet("me/status")]
    [ProducesResponseType(typeof(ApiResponse<AccountStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MyStatus()
    {
        var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(accountIdValue, out var accountId))
        {
            return Unauthorized();
        }

        var status = await _authService.GetAccountStatusAsync(accountId);
        if (status is null)
        {
            // Tài khoản không còn tồn tại -> coi như blocked để client đăng xuất.
            return Ok(ApiResponse<AccountStatusResponse>.Ok(
                new AccountStatusResponse { Status = "Blocked", Role = string.Empty }));
        }

        return Ok(ApiResponse<AccountStatusResponse>.Ok(status));
    }

    /// <summary>
    /// Đăng xuất: thu hồi refresh token để không thể refresh tiếp.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<LogoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        var result = await _authService.LogoutAsync(request);
        return Ok(ApiResponse<LogoutResponse>.Ok(result, result.Message));
    }
}
