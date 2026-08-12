namespace WebClient.Models.Interview;

// Backs Views/Interview/Index.cshtml - the interview configuration + start form.
public class DashboardViewModel
{
    public int DurationMinutes { get; set; } = 30;
    public string Level { get; set; } = "Junior";

    // Role, JD and focus technologies now drive the ai-service question plan (loaded from /catalog).
    public string Position { get; set; } = "Backend";
    public string? Jd { get; set; }

    // The CvJdAPI JobDescription backing Jd above — shared with Profile/CvJd (same record).
    public Guid? JdId { get; set; }
    public List<string> FocusAreas { get; set; } = new();

    // Newest completed/expired sessions shown in the sidebar "Recent Sessions" list.
    public List<SessionHistoryItemDto> RecentSessions { get; set; } = new();
    public InterviewSidebarViewModel Sidebar { get; set; } = new() { ActiveItem = "New" };

    // Current credit balance, shown next to the Duration options so the price/affordability of
    // each is clear before Start (see WebClient.Helpers.CreditPricing).
    //
    // NULL means "unknown" — PaymentAPI did not answer, or the user could not be resolved from the
    // auth cookie. It does NOT mean zero. This used to be a non-nullable int defaulting to 0, so an
    // outage rendered as "0 credits", disabled every duration, and told the user they could not
    // afford any interview while their wallet was actually fine. The view treats null as "don't
    // block": the authoritative check is InterviewAPI's DeductCreditAsync at Start, not this hint.
    public int? CreditBalance { get; set; }

    // Newest InProgress/Paused session, if any — drives the "resume your interview" banner.
    // Null means no active session (the normal case), not a failed lookup.
    public ActiveSessionResponse? ActiveSession { get; set; }

    public string? ErrorMessage { get; set; }
}