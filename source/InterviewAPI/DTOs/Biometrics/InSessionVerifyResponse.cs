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

    // True specifically for "no face in frame" — never a strike, just a gentle reminder.
    public bool NoFaceDetected { get; set; }

    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
}
