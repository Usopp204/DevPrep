namespace UserAPI.Helpers;

/// <summary>
/// Cung cấp thời gian hiện tại theo múi giờ Việt Nam (UTC+7).
/// Dùng chung để các mốc thời gian hiển thị/lưu trữ nhất quán theo giờ VN.
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    private static TimeZoneInfo ResolveTimeZone()
    {
        // Windows dùng id "SE Asia Standard Time", Linux/macOS dùng "Asia/Ho_Chi_Minh".
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        // Fallback: cố định UTC+7 nếu hệ điều hành không có sẵn timezone.
        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
