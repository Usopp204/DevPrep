using InterviewAPI.Models;

namespace InterviewAPI.Helpers;

public static class CheatingStrikeHelper
{
    public static int ComputeEffectiveStrikeCount(
        IEnumerable<SessionVerificationEvent> events,
        int noFaceStrikeThreshold)
    {
        var directStrikeCount = events.Count(e =>
            e.Phase == "InSession"
            && e.Decision == "Flag"
            && e.Modality is "Face" or "Voice" or "Camera" or "Check");
        var noFaceCount = events.Count(e =>
            e.Phase == "InSession"
            && e.Modality == "Face"
            && e.Decision == "NoFace");
        return directStrikeCount + noFaceCount / noFaceStrikeThreshold;
    }
}
