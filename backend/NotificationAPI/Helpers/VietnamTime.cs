namespace NotificationAPI.Helpers;

/// <summary>
/// Cung cấp thời gian hiện tại theo múi giờ Việt Nam (UTC+7).
/// </summary>
public static class VietnamTime
{
    private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

    /// <summary>Thời điểm hiện tại theo giờ Việt Nam.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    }
}
