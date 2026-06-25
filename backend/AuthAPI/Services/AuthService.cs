using System.Security.Cryptography;
using AuthAPI.DTOs;
using AuthAPI.Exceptions;
using AuthAPI.Helpers;
using AuthAPI.Models;
using AuthAPI.Repositories.Interfaces;
using AuthAPI.Services.Interfaces;
using DevPrep.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AuthAPI.Services;

public class AuthService : IAuthService
{
    private const string DefaultRole = "User";
    private const string AdminRole = "Admin";
    private const string ManagerRole = "Manager";
    private const string PendingStatus = "Pending";
    private const string ActiveStatus = "Active";
    private const string BlockedStatus = "Blocked";
    private const int OtpExpiryMinutes = 30;
    private const int ResetPasswordExpiryMinutes = 30;
    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;
    private const string DefaultResetPasswordUrl = "http://localhost:5173/reset-password";
    private const string PasswordMethod = "Password";
    private const string AdminMethod = "Admin";
    private const string SsoMethod = "SSO";

    private readonly IAccountRepository _accountRepository;
    private readonly IEmailVerificationTokenRepository _emailTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILoginAuditRepository _loginAuditRepository;
    private readonly ISsoIdentityRepository _ssoIdentityRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IGitHubOAuthClient _gitHubOAuthClient;
    private readonly IFirebaseTokenValidator _firebaseTokenValidator;
    private readonly INotificationApiClient _notificationApiClient;
    private readonly IUserApiClient _userApiClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAccountRepository accountRepository,
        IEmailVerificationTokenRepository emailTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILoginAuditRepository loginAuditRepository,
        ISsoIdentityRepository ssoIdentityRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IGoogleTokenValidator googleTokenValidator,
        IGitHubOAuthClient gitHubOAuthClient,
        IFirebaseTokenValidator firebaseTokenValidator,
        INotificationApiClient notificationApiClient,
        IUserApiClient userApiClient,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _accountRepository = accountRepository;
        _emailTokenRepository = emailTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _loginAuditRepository = loginAuditRepository;
        _ssoIdentityRepository = ssoIdentityRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _googleTokenValidator = googleTokenValidator;
        _gitHubOAuthClient = gitHubOAuthClient;
        _firebaseTokenValidator = firebaseTokenValidator;
        _notificationApiClient = notificationApiClient;
        _userApiClient = userApiClient;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // Chuẩn hoá email để so sánh và lưu trữ nhất quán.
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _accountRepository.ExistsByEmailAsync(email))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var account = new Account
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = DefaultRole,
            Status = PendingStatus,
            IsEmailVerified = false
        };
        await _accountRepository.AddAsync(account);

        // Sinh OTP xác thực email. Gán navigation property để EF tự điền AccountId
        // sau khi insert Account (cả hai được lưu trong cùng một transaction).
        var otp = GenerateOtp();
        var expiresAt = VietnamTime.Now.AddMinutes(OtpExpiryMinutes);
        var verificationToken = new EmailVerificationToken
        {
            Account = account,
            Token = otp,
            ExpiresAt = expiresAt,
            IsUsed = false
        };
        await _emailTokenRepository.AddAsync(verificationToken);

        try
        {
            await _accountRepository.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Phòng trường hợp 2 request đăng ký cùng email chạy đồng thời:
            // unique index trên Email sẽ chặn bản ghi thứ hai.
            throw new ConflictException("An account with this email already exists.");
        }

        // Phát sự kiện để NotificationAPI gửi email chứa OTP (qua RabbitMQ/MassTransit).
        await PublishEmailVerificationAsync(account.AccountId, email, otp, expiresAt);

        // Phát sự kiện để UserAPI tạo hồ sơ (UserProfiles) cho tài khoản mới.
        await PublishAccountRegisteredAsync(account.AccountId, email, request.FullName);

        return new RegisterResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Status = account.Status,
            IsEmailVerified = account.IsEmailVerified
        };
    }

    public async Task<VerifyEmailOtpResponse> VerifyEmailOtpAsync(VerifyEmailOtpRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var account = await _accountRepository.GetByEmailAsync(email)
            ?? throw new NotFoundException("Account not found.");

        if (account.IsEmailVerified)
        {
            throw new BadRequestException("This email has already been verified.");
        }

        var token = await _emailTokenRepository.GetValidTokenAsync(account.AccountId, request.Otp)
            ?? throw new BadRequestException("The OTP is invalid or has expired.");

        // Đánh dấu OTP đã dùng và kích hoạt tài khoản.
        token.IsUsed = true;
        account.IsEmailVerified = true;
        account.Status = ActiveStatus;
        account.UpdatedAt = VietnamTime.Now;

        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Email verified successfully for {Email}.", email);

        return new VerifyEmailOtpResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            IsEmailVerified = account.IsEmailVerified,
            Status = account.Status
        };
    }

    public async Task<ResendEmailOtpResponse> ResendEmailOtpAsync(ResendEmailOtpRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var account = await _accountRepository.GetByEmailAsync(email)
            ?? throw new NotFoundException("Account not found.");

        if (account.IsEmailVerified)
        {
            throw new BadRequestException("This email has already been verified.");
        }

        // Vô hiệu hoá các OTP cũ chưa dùng để chỉ còn duy nhất một OTP hợp lệ.
        var unusedTokens = await _emailTokenRepository.GetUnusedTokensByAccountAsync(account.AccountId);
        foreach (var oldToken in unusedTokens)
        {
            oldToken.IsUsed = true;
        }

        var otp = GenerateOtp();
        var expiresAt = VietnamTime.Now.AddMinutes(OtpExpiryMinutes);
        var newToken = new EmailVerificationToken
        {
            AccountId = account.AccountId,
            Token = otp,
            ExpiresAt = expiresAt,
            IsUsed = false
        };
        await _emailTokenRepository.AddAsync(newToken);

        await _accountRepository.SaveChangesAsync();

        // Phát sự kiện để NotificationAPI gửi lại email chứa OTP.
        await PublishEmailVerificationAsync(account.AccountId, email, otp, expiresAt);

        return new ResendEmailOtpResponse
        {
            Email = account.Email,
            ExpiresAt = expiresAt
        };
    }

    public Task<LoginResponse> LoginAsync(LoginRequest request) =>
        LoginCoreAsync(request, adminOnly: false);

    public Task<LoginResponse> AdminLoginAsync(LoginRequest request) =>
        LoginCoreAsync(request, adminOnly: true);

    /// <summary>
    /// Logic đăng nhập dùng chung. <paramref name="adminOnly"/> = true thì chỉ chấp nhận Admin/Manager.
    /// </summary>
    private async Task<LoginResponse> LoginCoreAsync(LoginRequest request, bool adminOnly)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var auditMethod = adminOnly ? AdminMethod : PasswordMethod;
        var account = await _accountRepository.GetByEmailAsync(email);

        // Không tồn tại -> trả lỗi chung (tránh lộ email nào đã đăng ký).
        if (account is null)
        {
            await AddAuditAsync(null, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            throw new UnauthorizedException("Invalid email or password.");
        }

        // Bị admin khoá vĩnh viễn (Status = Blocked) -> trả kèm lý do nếu có.
        if (account.Status == BlockedStatus)
        {
            await AddAuditAsync(account.AccountId, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            var reason = string.IsNullOrWhiteSpace(account.BlockedReason)
                ? string.Empty
                : $" Reason: {account.BlockedReason}";
            throw new ForbiddenException($"This account has been blocked.{reason}");
        }

        // Đang bị khoá tạm thời do đăng nhập sai nhiều lần (so sánh theo giờ VN).
        if (account.LockedUntil.HasValue && account.LockedUntil.Value > VietnamTime.Now)
        {
            await AddAuditAsync(account.AccountId, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            var remaining = (int)Math.Ceiling((account.LockedUntil.Value - VietnamTime.Now).TotalMinutes);
            throw new UnauthorizedException(
                $"Account is temporarily locked due to multiple failed login attempts. Please try again in {remaining} minute(s).");
        }

        // Sai mật khẩu -> tăng số lần thất bại, khoá tạm nếu vượt ngưỡng.
        if (string.IsNullOrEmpty(account.PasswordHash) || !_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount += 1;
            if (account.FailedLoginCount >= MaxFailedAttempts)
            {
                // Lưu thời điểm hết khoá theo giờ Việt Nam (UTC+7).
                account.LockedUntil = VietnamTime.Now.AddMinutes(LockoutMinutes);
                account.FailedLoginCount = 0;
            }
            account.UpdatedAt = VietnamTime.Now;
            await AddAuditAsync(account.AccountId, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            throw new UnauthorizedException("Invalid email or password.");
        }

        // Chưa xác thực email.
        if (!account.IsEmailVerified)
        {
            await AddAuditAsync(account.AccountId, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            throw new ForbiddenException("Please verify your email before logging in.");
        }

        // Đăng nhập admin nhưng tài khoản không phải Admin/Manager.
        if (adminOnly && account.Role != AdminRole && account.Role != ManagerRole)
        {
            await AddAuditAsync(account.AccountId, email, isSuccess: false, auditMethod);
            await _accountRepository.SaveChangesAsync();
            throw new ForbiddenException("This account is not allowed to access the admin area.");
        }

        // Thành công.
        return await IssueLoginAsync(account, auditMethod);
    }

    public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request)
    {
        var info = await _googleTokenValidator.ValidateAsync(request.IdToken);
        return await ExternalLoginAsync(info);
    }

    public async Task<LoginResponse> GitHubLoginAsync(GitHubLoginRequest request)
    {
        var info = await _gitHubOAuthClient.GetUserAsync(request.Code, request.RedirectUri);
        return await ExternalLoginAsync(info);
    }

    public async Task<LoginResponse> FirebaseLoginAsync(FirebaseLoginRequest request)
    {
        // Verify Firebase ID token (chữ ký Google + issuer/audience/hết hạn) rồi rút thông tin đã xác minh.
        // Dùng chung cho cả Google lẫn GitHub (Firebase cấp cùng kiểu ID token).
        var info = await _firebaseTokenValidator.ValidateAsync(request.IdToken);
        return await ExternalLoginAsync(info);
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var account = await _accountRepository.GetByEmailAsync(email);

        // Chỉ tạo token + gửi email khi tài khoản tồn tại và chưa bị khoá vĩnh viễn.
        // Dù trường hợp nào cũng trả về CÙNG một thông báo để không lộ email nào đã
        // đăng ký (chống dò tài khoản - account enumeration).
        if (account is not null && account.Status != BlockedStatus)
        {
            // Vô hiệu hoá các token reset cũ chưa dùng -> chỉ còn duy nhất một link hợp lệ.
            var oldTokens = await _passwordResetTokenRepository.GetUnusedTokensByAccountAsync(account.AccountId);
            foreach (var old in oldTokens)
            {
                old.IsUsed = true;
            }

            var token = GenerateSecureToken();
            var expiresAt = VietnamTime.Now.AddMinutes(ResetPasswordExpiryMinutes);
            await _passwordResetTokenRepository.AddAsync(new PasswordResetToken
            {
                AccountId = account.AccountId,
                Token = token,
                ExpiresAt = expiresAt,
                IsUsed = false
            });
            await _accountRepository.SaveChangesAsync();

            await PublishPasswordResetAsync(account.AccountId, email, token, expiresAt);
        }
        else
        {
            _logger.LogInformation(
                "Forgot-password yêu cầu cho email không tồn tại hoặc đã bị khoá: {Email}. Không gửi email.",
                email);
        }

        return new ForgotPasswordResponse
        {
            Message = "If an account with that email exists, a password reset link has been sent."
        };
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var resetToken = await _passwordResetTokenRepository.GetValidByTokenAsync(request.Token)
            ?? throw new BadRequestException("The reset link is invalid or has expired.");

        var account = resetToken.Account;

        // Không cho đặt lại mật khẩu cho tài khoản đang bị admin khoá.
        if (account.Status == BlockedStatus)
        {
            throw new ForbiddenException("This account has been blocked.");
        }

        // Đặt mật khẩu mới, vô hiệu hoá token, gỡ khoá đăng nhập sai (nếu có).
        account.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        account.FailedLoginCount = 0;
        account.LockedUntil = null;
        account.UpdatedAt = VietnamTime.Now;
        resetToken.IsUsed = true;

        // Thu hồi mọi refresh token đang hoạt động -> buộc đăng nhập lại trên các thiết bị
        // khác (phòng trường hợp tài khoản đã bị chiếm dụng phiên đăng nhập).
        await _refreshTokenRepository.RevokeAllActiveByAccountAsync(account.AccountId);

        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Đặt lại mật khẩu thành công cho {Email}.", account.Email);

        return new ResetPasswordResponse
        {
            Email = account.Email,
            Message = "Your password has been reset successfully. Please log in with your new password."
        };
    }

    public async Task<ChangePasswordResponse> ChangePasswordAsync(Guid accountId, ChangePasswordRequest request)
    {
        var account = await _accountRepository.GetByIdAsync(accountId)
            ?? throw new NotFoundException("Account not found.");

        if (account.Status == BlockedStatus)
        {
            throw new ForbiddenException("This account has been blocked.");
        }

        // Tài khoản chỉ-SSO chưa có mật khẩu -> không đổi được, phải dùng quên mật khẩu để đặt.
        if (string.IsNullOrEmpty(account.PasswordHash))
        {
            throw new BadRequestException("This account has no password set. Please use forgot-password to set one.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
        {
            throw new BadRequestException("Current password is incorrect.");
        }

        // Mật khẩu mới phải khác mật khẩu hiện tại.
        if (_passwordHasher.Verify(request.NewPassword, account.PasswordHash))
        {
            throw new BadRequestException("New password must be different from the current password.");
        }

        account.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        account.UpdatedAt = VietnamTime.Now;

        // Thu hồi mọi refresh token đang hoạt động -> đăng xuất các thiết bị khác.
        await _refreshTokenRepository.RevokeAllActiveByAccountAsync(account.AccountId);

        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Đổi mật khẩu thành công cho {Email}.", account.Email);

        return new ChangePasswordResponse
        {
            Email = account.Email,
            Message = "Password changed successfully."
        };
    }

    public async Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        // Không tồn tại / đã thu hồi / đã hết hạn -> từ chối.
        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= VietnamTime.Now)
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        var account = stored.Account;
        if (account.Status == BlockedStatus)
        {
            var reason = string.IsNullOrWhiteSpace(account.BlockedReason)
                ? string.Empty
                : $" Reason: {account.BlockedReason}";
            throw new ForbiddenException($"This account has been blocked.{reason}");
        }

        // Xoay vòng (rotation): thu hồi refresh token cũ rồi phát refresh token mới.
        stored.IsRevoked = true;
        stored.RevokedAt = VietnamTime.Now;

        var (accessToken, expiresIn) = _tokenService.GenerateAccessToken(account);
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        var (ipAddress, userAgent) = GetClientInfo();
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            AccountId = account.AccountId,
            Token = newRefreshToken,
            ExpiresAt = VietnamTime.Now.AddDays(_tokenService.RefreshTokenDays),
            IsRevoked = false,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });

        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Đã cấp lại access token cho {Email}.", account.Email);

        return new LoginResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Role = account.Role,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn
        };
    }

    public async Task<LogoutResponse> LogoutAsync(LogoutRequest request)
    {
        var stored = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        // Idempotent: token không tồn tại / đã thu hồi -> vẫn coi như đăng xuất thành công
        // (không tiết lộ token có hợp lệ hay không).
        if (stored is not null && !stored.IsRevoked)
        {
            stored.IsRevoked = true;
            stored.RevokedAt = VietnamTime.Now;
            await _accountRepository.SaveChangesAsync();
            _logger.LogInformation("Đăng xuất (thu hồi refresh token) cho account {AccountId}.", stored.AccountId);
        }

        return new LogoutResponse { Message = "Logged out successfully." };
    }

    /// <summary>
    /// Xử lý đăng nhập SSO dùng chung cho mọi nhà cung cấp: tìm liên kết sẵn có,
    /// hoặc liên kết vào tài khoản trùng email, hoặc tạo tài khoản mới.
    /// </summary>
    private async Task<LoginResponse> ExternalLoginAsync(ExternalUserInfo info)
    {
        var existing = await _ssoIdentityRepository.GetByProviderAsync(info.Provider, info.ProviderUserId);
        Account account;

        if (existing is not null)
        {
            // Đã từng đăng nhập bằng nhà cung cấp này -> dùng lại tài khoản đã liên kết.
            account = await _accountRepository.GetByIdAsync(existing.AccountId)
                ?? throw new NotFoundException("Linked account not found.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(info.Email) || !info.EmailVerified)
            {
                throw new BadRequestException("Your provider account does not have a verified email to sign in with.");
            }

            var email = info.Email.Trim().ToLowerInvariant();
            var matched = await _accountRepository.GetByEmailAsync(email);

            if (matched is null)
            {
                // Tạo tài khoản mới chỉ-SSO (không có mật khẩu, email coi như đã verify).
                account = new Account
                {
                    Email = email,
                    PasswordHash = null,
                    Role = DefaultRole,
                    Status = ActiveStatus,
                    IsEmailVerified = true
                };
                await _accountRepository.AddAsync(account);
            }
            else
            {
                // Liên kết vào tài khoản sẵn có; nhà cung cấp đã xác minh email.
                account = matched;
                if (!account.IsEmailVerified)
                {
                    account.IsEmailVerified = true;
                    account.UpdatedAt = VietnamTime.Now;
                }
            }

            await _ssoIdentityRepository.AddAsync(new SsoIdentity
            {
                Account = account,
                Provider = info.Provider,
                ProviderUserId = info.ProviderUserId,
                ProviderEmail = info.Email
            });

            // Lưu để tài khoản mới có AccountId trước khi phát token.
            await _accountRepository.SaveChangesAsync();

            // Nếu vừa tạo tài khoản mới qua SSO -> phát sự kiện để UserAPI tạo hồ sơ.
            if (matched is null)
            {
                await PublishAccountRegisteredAsync(account.AccountId, account.Email, info.Name);
            }
        }

        // Chặn nếu tài khoản bị khóa.
        if (account.Status == BlockedStatus)
        {
            await AddAuditAsync(account.AccountId, account.Email, isSuccess: false, SsoMethod);
            await _accountRepository.SaveChangesAsync();
            var reason = string.IsNullOrWhiteSpace(account.BlockedReason)
                ? string.Empty
                : $" Reason: {account.BlockedReason}";
            throw new ForbiddenException($"This account has been blocked.{reason}");
        }

        return await IssueLoginAsync(account, SsoMethod);
    }

    /// <summary>
    /// Phát access/refresh token cho một tài khoản đã xác thực thành công (dùng chung
    /// cho login mật khẩu và SSO). Cập nhật lần đăng nhập, ghi audit và lưu thay đổi.
    /// </summary>
    private async Task<LoginResponse> IssueLoginAsync(Account account, string method)
    {
        account.FailedLoginCount = 0;
        account.LockedUntil = null;
        account.LastLoginAt = VietnamTime.Now;
        account.UpdatedAt = VietnamTime.Now;

        var (accessToken, expiresIn) = _tokenService.GenerateAccessToken(account);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var (ipAddress, userAgent) = GetClientInfo();
        await _refreshTokenRepository.AddAsync(new RefreshToken
        {
            AccountId = account.AccountId,
            Token = refreshToken,
            ExpiresAt = VietnamTime.Now.AddDays(_tokenService.RefreshTokenDays),
            IsRevoked = false,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });

        await AddAuditAsync(account.AccountId, account.Email, isSuccess: true, method);
        await _accountRepository.SaveChangesAsync();

        _logger.LogInformation("Login success for {Email} (role {Role}, method {Method}).", account.Email, account.Role, method);

        return new LoginResponse
        {
            AccountId = account.AccountId,
            Email = account.Email,
            Role = account.Role,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn
        };
    }

    /// <summary>
    /// Phát sự kiện yêu cầu gửi email OTP. Nếu broker lỗi, vẫn cho luồng đăng ký thành công
    /// (OTP đã lưu DB, người dùng có thể gọi resend). Đồng thời log OTP để tiện kiểm thử.
    /// </summary>
    private async Task PublishEmailVerificationAsync(Guid accountId, string email, string otp, DateTime expiresAt)
    {
        _logger.LogInformation(
            "Email verification OTP for {Email}: {Otp} (expires in {Minutes} minutes)",
            email, otp, OtpExpiryMinutes);

        try
        {
            await _notificationApiClient.SendEmailVerificationAsync(new EmailVerificationRequested
            {
                AccountId = accountId,
                Email = email,
                Otp = otp,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call NotificationAPI (email verification) for {Email}.", email);
        }
    }

    /// <summary>
    /// Phát sự kiện yêu cầu gửi email đặt lại mật khẩu. Link trỏ về trang frontend
    /// (URL cấu hình qua FRONTEND_RESET_PASSWORD_URL) kèm token. Log link để tiện kiểm thử.
    /// </summary>
    private async Task PublishPasswordResetAsync(Guid accountId, string email, string token, DateTime expiresAt)
    {
        var baseUrl = _configuration.GetValue<string>("FRONTEND_RESET_PASSWORD_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultResetPasswordUrl;
        }
        var resetUrl = $"{baseUrl}?token={Uri.EscapeDataString(token)}";

        _logger.LogInformation(
            "Password reset link cho {Email}: {ResetUrl} (hết hạn sau {Minutes} phút)",
            email, resetUrl, ResetPasswordExpiryMinutes);

        try
        {
            await _notificationApiClient.SendPasswordResetAsync(new PasswordResetRequested
            {
                AccountId = accountId,
                Email = email,
                ResetUrl = resetUrl,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call NotificationAPI (password reset) for {Email}.", email);
        }
    }

    /// <summary>
    /// Phát sự kiện AccountRegistered để UserAPI tạo hồ sơ (UserProfiles) cho tài khoản mới.
    /// Best-effort: nếu broker lỗi vẫn không chặn luồng đăng ký (hồ sơ có thể tạo lại sau).
    /// </summary>
    private async Task PublishAccountRegisteredAsync(Guid accountId, string email, string? fullName)
    {
        try
        {
            await _userApiClient.CreateProfileAsync(new AccountRegistered
            {
                AccountId = accountId,
                Email = email,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim()
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call UserAPI (create profile) for {Email}.", email);
        }
    }

    private Task AddAuditAsync(Guid? accountId, string email, bool isSuccess, string method)
    {
        var (ipAddress, userAgent) = GetClientInfo();
        return _loginAuditRepository.AddAsync(new LoginAudit
        {
            AccountId = accountId,
            Email = email,
            IsSuccess = isSuccess,
            Method = method,
            IpAddress = ipAddress,
            UserAgent = userAgent
        });
    }

    private (string? ipAddress, string? userAgent) GetClientInfo()
    {
        var context = _httpContextAccessor.HttpContext;
        var ipAddress = context?.Connection.RemoteIpAddress?.ToString();
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrEmpty(userAgent) && userAgent.Length > 512)
        {
            userAgent = userAgent[..512];
        }
        return (ipAddress, string.IsNullOrEmpty(userAgent) ? null : userAgent);
    }

    /// <summary>
    /// Sinh mã OTP 6 chữ số an toàn về mặt mật mã học.
    /// </summary>
    private static string GenerateOtp() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>
    /// Sinh token ngẫu nhiên 32 byte, mã hoá Base64 URL-safe (dùng cho link đặt lại mật khẩu).
    /// </summary>
    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
