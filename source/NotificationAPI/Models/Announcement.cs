using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotificationAPI.Models;

/// <summary>
/// Thông báo hệ thống do admin tạo — hiển thị popup trên homepage cho guest + user.
/// Soft-delete (IsDeleted); IsActive = bật/tắt hiển thị mà không cần xoá.
/// </summary>
[Table("Announcements")]
public partial class Announcement
{
    [Key]
    public Guid AnnouncementId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    [StringLength(2000)]
    public string Content { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
