namespace WebClient.Models.Interview;

// The camera violation reason the Room reports (same-origin proxy to the Gateway).
public class CameraViolationProxyRequest
{
    public Guid IncidentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CameraViolationResultModel
{
    public int CameraViolationCount { get; set; }
    public int StrikeCount { get; set; }
    public int MaxStrikes { get; set; }
    public bool Duplicate { get; set; }
    public bool ShouldEndSession { get; set; }
    public string Message { get; set; } = string.Empty;
}
