namespace FeedbackAPI.Helpers;

/// <summary>
/// Thời gian theo giờ Việt Nam (UTC+7). Dùng cho UpdatedAt/DeletedAt khi cập nhật/xoá,
/// đồng bộ với DEFAULT của DB (DATEADD(HOUR,7,SYSUTCDATETIME())).
/// </summary>
public static class VietnamTime
{
    public static DateTime Now => DateTime.UtcNow.AddHours(7);
}
