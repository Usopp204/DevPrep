namespace InterviewAPI.DTOs.Session;

public class ReportCameraViolationRequest
{
    public Guid IncidentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
