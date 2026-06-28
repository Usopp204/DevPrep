using UserAPI.Services.Interfaces;

namespace UserAPI.Services;

/// <summary>
/// Lưu file vào thư mục wwwroot/avatars và phục vụ qua static files.
/// URL công khai = {FILE_PUBLIC_BASE_URL}/avatars/{tên file}.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private const string AvatarFolder = "avatars";
    private const string DefaultBaseUrl = "http://localhost:7002";

    private readonly string _webRoot;
    private readonly string _publicBaseUrl;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<LocalFileStorageService> logger)
    {
        // WebRootPath có thể null nếu wwwroot chưa tồn tại lúc khởi động -> tự suy ra.
        _webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var baseUrl = configuration.GetValue<string>("FILE_PUBLIC_BASE_URL");
        _publicBaseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl).TrimEnd('/');
        _logger = logger;
    }

    public async Task<string> SaveAvatarAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(_webRoot, AvatarFolder);
        Directory.CreateDirectory(folder);

        // Tên file ngẫu nhiên để tránh trùng và tránh path traversal từ tên gốc.
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"{_publicBaseUrl}/{AvatarFolder}/{fileName}";
    }

    public void DeleteAvatar(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        // Chỉ xoá nếu URL trỏ tới thư mục avatars do service này quản lý.
        var marker = $"/{AvatarFolder}/";
        var idx = avatarUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return;
        }

        var fileName = avatarUrl[(idx + marker.Length)..];
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return;
        }

        try
        {
            var fullPath = Path.Combine(_webRoot, AvatarFolder, fileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không xoá được ảnh đại diện cũ: {Url}", avatarUrl);
        }
    }
}
