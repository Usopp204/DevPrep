using System.Net.Http.Json;
using WebClient.Models.Home;
using WebClient.Services.Shared;

namespace WebClient.Services.Home;

public interface IAnnouncementService
{
    /// <summary>Thông báo đang bật cho popup homepage. Best-effort: lỗi -> danh sách rỗng.</summary>
    Task<List<AnnouncementItem>> GetActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Gọi NotificationAPI qua gateway (route /api/notifications strip prefix). Endpoint public,
/// guest không có cookie token vẫn gọi được (JwtDelegatingHandler chỉ gắn Bearer khi có cookie).
/// </summary>
public sealed class AnnouncementService(IApiClient apiClient) : IAnnouncementService
{
    public async Task<List<AnnouncementItem>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await apiClient.HttpClient.GetFromJsonAsync<List<AnnouncementItem>>(
                "api/notifications/announcements", cancellationToken);
            return items ?? new List<AnnouncementItem>();
        }
        catch
        {
            // Best-effort: NotificationAPI/gateway chết thì homepage vẫn render bình thường.
            return new List<AnnouncementItem>();
        }
    }
}
