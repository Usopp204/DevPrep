namespace StatisticsAPI.Helpers;

/// <summary>Giờ Việt Nam (UTC+7) — dùng chung cho mọi mốc thời gian nghiệp vụ (trừ JWT exp giữ UTC).</summary>
public static class VietnamTime
{
    public static DateTime Now => DateTime.UtcNow.AddHours(7);
    public static DateTime Today => Now.Date;
}
