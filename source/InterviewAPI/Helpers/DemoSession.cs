namespace InterviewAPI.Helpers;

// DEMO-SESSION: remove after defence — start
//
// TEMPORARY CODE FOR ONE PRESENTATION. Delete this whole file when the defence is over, then
// follow docs/architecture/demo-session-removal.md for the handful of call sites that reference it.
//
// A short interview used to demonstrate the full system to a review board inside a few minutes:
// 5 minutes, exactly 3 questions, 6 credits. Every other feature behaves normally — biometric
// gate, camera enforcement, per-question face checks, hints, skip, pause, TTS, STT and background
// grading all read session.DurationMinutes / TotalQuestions and need no demo-specific handling.
//
// Questions are still produced by the real RAG pipeline. ai-service plans its normal pool for a
// 5-minute request (target_questions(5) => 8 questions) and InterviewAPI keeps the first
// QuestionCount of them. Nothing is scripted or pre-canned.
//
// Guarded by DEMO_SESSION_ENABLED (default false) so it cannot be reached in normal operation.
// Without the guard, users would rationally always pick the 6-credit option over the 30-credit
// one and the pricing model would collapse.
public static class DemoSession
{
    public const int DurationMinutes = 5;
    public const int QuestionCount = 3;
    public const int CreditCost = 6;

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("DEMO_SESSION_ENABLED", false);

    /// <summary>
    /// True only for the demo duration AND only while the flag is on. Both halves matter: with the
    /// flag off, 5 must be rejected as an invalid duration exactly as it was before this existed.
    /// </summary>
    public static bool IsDemoDuration(IConfiguration configuration, int durationMinutes) =>
        durationMinutes == DurationMinutes && IsEnabled(configuration);
}
// DEMO-SESSION: remove after defence — end
