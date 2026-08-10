using InterviewAPI.Models;

namespace InterviewAPI.Helpers;

public static class CheatingStrikeHelper
{
    public static int ComputeEffectiveStrikeCount(
        IEnumerable<SessionVerificationEvent> events,
        int noFaceStrikeThreshold,
        int tabStrikeThreshold)
    {
        var directStrikeCount = events.Count(e =>
            e.Phase == "InSession"
            && e.Decision == "Flag"
            && e.Modality is "Face" or "Voice" or "Camera" or "Check" or "Paste");
        var noFaceCount = events.Count(e =>
            e.Phase == "InSession"
            && e.Modality == "Face"
            && e.Decision == "NoFace");
        // "Tab" is deliberately NOT in the direct-strike whitelist above — same treatment as
        // "Face"/NoFace: it accumulates toward a strike on its own conversion cadence
        // (tabStrikeThreshold) instead of counting every single event as a full strike.
        var tabCount = events.Count(e =>
            e.Phase == "InSession"
            && e.Modality == "Tab"
            && e.Decision == "Flag");
        return directStrikeCount
            + noFaceCount / noFaceStrikeThreshold
            + tabCount / tabStrikeThreshold;
    }

    // Shared termination predicate for every in-session strike source (Face mismatch/multi-face,
    // Voice mismatch, Camera violation, a missed check, Paste). A session ends only when THIS
    // call is the one that produced a new strike (strikeJustHappened) AND Strictness="Strict" AND
    // the cap has actually been reached. strikeJustHappened matters because effectiveStrikeCount
    // can keep growing past maxStrikes under Strictness="Flag" (nothing there ever ends the
    // session) — without it, a session already sitting at/above the cap would terminate on the
    // next unrelated read the instant strictness was switched to "Strict", instead of only on the
    // call that actually crossed the line.
    public static bool ShouldEndSession(
        bool strikeJustHappened, string? strictness, int effectiveStrikeCount, int maxStrikes)
    {
        return strikeJustHappened
            && string.Equals(strictness, "Strict", StringComparison.OrdinalIgnoreCase)
            && effectiveStrikeCount >= maxStrikes;
    }
}
