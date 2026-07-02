using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserAPI.DTOs;
using UserAPI.Services.Interfaces;

namespace UserAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private static readonly string[] AllowedAvatarExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxAvatarBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IUserProfileService _profileService;

    public UsersController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    /// <summary>
    /// Xem hồ sơ của chính người dùng đang đăng nhập (cần JWT access token).
    /// Trả về Email (từ token), FullName và Avatar (từ UserProfiles).
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
    {
        // AccountId nằm ở claim NameIdentifier (hoặc "sub"); Email ở claim Email.
        var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? string.Empty;

        if (!Guid.TryParse(accountIdValue, out var accountId))
        {
            return Unauthorized();
        }

        var result = await _profileService.GetMyProfileAsync(accountId, email);
        return Ok(ApiResponse<ProfileResponse>.Ok(result));
    }

    /// <summary>
    /// Cập nhật hồ sơ của chính người dùng: đổi FullName và (tuỳ chọn) ảnh đại diện
    /// bằng cách chọn ảnh từ máy. Gửi dạng multipart/form-data.
    /// </summary>
    [HttpPut("me")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile([FromForm] UpdateProfileRequest request)
    {
        // Kiểm tra file ảnh (nếu có): định dạng + dung lượng.
        if (request.Avatar is { } avatar)
        {
            if (avatar.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Avatar file is empty."));
            }
            if (avatar.Length > MaxAvatarBytes)
            {
                return BadRequest(ApiResponse<object>.Fail("Avatar must not exceed 5 MB."));
            }
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!AllowedAvatarExtensions.Contains(ext))
            {
                return BadRequest(ApiResponse<object>.Fail("Avatar must be an image (jpg, jpeg, png, gif, webp)."));
            }
        }

        var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? string.Empty;

        if (!Guid.TryParse(accountIdValue, out var accountId))
        {
            return Unauthorized();
        }

        var result = await _profileService.UpdateMyProfileAsync(accountId, email, request.FullName, request.Avatar);
        return Ok(ApiResponse<ProfileResponse>.Ok(result, "Profile updated successfully."));
    }
}
