namespace InterviewAPI.DTOs.Biometrics;

// Returned by POST api/interview/sessions/{sessionId}/verify/in-session-check — the result of
// one background cheating check (face match against the enrolled template + multi-face check).
// Flag-only by default (Biometrics:InSessionStrictness="Flag"): StrikeCount/MaxStrikes are
// informational and ShouldEndSession is always false unless Strictness="Strict" AND the strike
// cap was just reached by THIS call.
public class InSessionVerifyResponse
{
    // True when the face matched the enrolled template and exactly one face was in frame — no
    // strike either way. False whenever a strike (mismatch or multiple faces) OR a no-face
    // no-strike event was recorded — see Message for which one.
    public bool Passed { get; set; }

    // True specifically for "no face in frame". Not a strike on its own — see NoFaceCount/
    // NoFaceStrikeThreshold; it only converts into a strike once accumulated NoFace events hit
    // the threshold (see BiometricVerificationService.VerifyInSessionAsync).
    public bool NoFaceDetected { get; set; }

    // "None" | "NoFace" | "IdentityMismatch" | "MultiFace" | "ImageTooBlurry" |
    // "FaceTooSmall" | "LowDetectionConfidence" | "CannotDecode" — lets the client render
    // cheating strikes separately from non-strike camera-quality guidance.
    public string WarningType { get; set; } = "None";

    // Always clamped to [0, MaxStrikes] for display — the real accumulated count (Flag events
    // plus converted NoFace groups) can keep growing past the cap when Strictness="Flag" (no
    // auto-end), but the UI must never show e.g. "4/3".
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }

    // Progress toward the NEXT NoFace-to-strike conversion (0..NoFaceStrikeThreshold), reset
    // after each conversion. Only meaningful while WarningType=="NoFace".
    public int NoFaceCount { get; set; }
    public int NoFaceStrikeThreshold { get; set; }

    // True only on the exact check where accumulated NoFace events just completed a group of
    // NoFaceStrikeThreshold and converted into a real strike — lets the client switch from the
    // gentle "you're stepping out of frame" reminder to the same urgent styling a Flag gets,
    // without having to parse Message to tell the two NoFace cases apart.
    public bool NoFaceJustBecameStrike { get; set; }

    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
}
