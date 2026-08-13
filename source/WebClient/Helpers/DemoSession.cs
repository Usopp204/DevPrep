namespace WebClient.Helpers;

// DEMO-SESSION: remove after defence — start
//
// TEMPORARY CODE FOR ONE PRESENTATION. Delete this whole file when the defence is over, then
// follow docs/architecture/demo-session-removal.md.
//
// WebClient's half of the demo session: it only needs to know whether to offer the extra duration
// in the Start-interview dropdown. All enforcement (valid duration, question count, credit cost)
// lives server-side in InterviewAPI's own DemoSession helper — this is presentation only, and
// turning the flag off here without turning it off there would simply hide an option that
// InterviewAPI would still accept.
//
// Values are duplicated rather than shared because WebClient and InterviewAPI have no common
// project reference; keep them in step with source/InterviewAPI/Helpers/DemoSession.cs for as long
// as this exists. That duplication is deliberate and short-lived — both files are deleted together.
public static class DemoSession
{
    public const int DurationMinutes = 5;
    public const int CreditCost = 6;

    /// <summary>
    /// Resolved from the request's service provider rather than injected, so that every trace of
    /// the demo lives inside one deletable block in the view instead of an @inject line elsewhere.
    /// </summary>
    public static bool IsEnabled(IServiceProvider services) =>
        (services.GetService(typeof(IConfiguration)) as IConfiguration)
            ?.GetValue("DEMO_SESSION_ENABLED", false) ?? false;
}
// DEMO-SESSION: remove after defence — end
