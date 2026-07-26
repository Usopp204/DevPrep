namespace InterviewAPI.DTOs.Session;

// reason: "PermissionDenied" | "Disconnected" — free-form-ish but the client only ever sends
// these two today; stored as-is in the audit log, not validated against a fixed enum.
public class ReportCameraViolationRequest
{
    public string Reason { get; set; } = string.Empty;
}
