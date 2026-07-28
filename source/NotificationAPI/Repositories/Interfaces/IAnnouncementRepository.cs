using NotificationAPI.Models;

namespace NotificationAPI.Repositories.Interfaces;

public interface IAnnouncementRepository
{
    /// <summary>Thông báo đang bật + chưa xoá, mới nhất trước (cho popup homepage).</summary>
    Task<List<Announcement>> GetActiveAsync(int maxCount);

    /// <summary>Danh sách cho trang admin: mặc định chưa xoá; onlyDeleted=true -> chỉ bản đã xoá mềm.</summary>
    Task<List<Announcement>> GetAdminListAsync(bool onlyDeleted, int maxCount);

    Task<Announcement?> GetTrackedByIdAsync(Guid id);

    Task AddAsync(Announcement announcement);

    Task<int> SaveChangesAsync();
}
