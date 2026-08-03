namespace WebClient.Helpers;

// Formats a timestamp as a short relative label ("2h ago", "Yesterday").
//
// Session timestamps from InterviewAPI are stored as Vietnam wall-clock time (UTC+7, via
// VietnamTime.Now) and serialize with a Utc tag, so "now" must be taken in the same UTC+7 frame
// (DateTime.UtcNow.AddHours(7)) for the elapsed span to be correct regardless of the WebClient
// host's own timezone. Only ticks are subtracted, so the +7h offset cancels out either way.
public static class TimeFormat
{
    public static string RelativeTime(DateTime timestamp)
    {
        var now = DateTime.UtcNow.AddHours(7);
        var delta = now - timestamp;

        if (delta.TotalMinutes < 1) return "Just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalHours < 48) return "Yesterday";
        if (delta.TotalDays < 7) return $"{(int)delta.TotalDays}d ago";
        return timestamp.ToString("MMM d", DisplayCulture.Neutral);
    }
}
