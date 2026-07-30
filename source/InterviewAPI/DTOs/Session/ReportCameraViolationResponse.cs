namespace InterviewAPI.DTOs.Session;

public class ReportCameraViolationResponse
{
    public int CameraViolationCount { get; set; }
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public bool Duplicate { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
}
