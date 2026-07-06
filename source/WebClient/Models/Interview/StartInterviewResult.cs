namespace WebClient.Models.Interview;

// Outcome of running the config-duration -> config-level -> config-interaction -> init ->
// generate-question sequence against InterviewAPI. Room re-fetches full state via Resume,
// so only the new SessionId is needed here.
public class StartInterviewResult
{
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid SessionId { get; private init; }

    public static StartInterviewResult Failure(string message) =>
        new() { Success = false, ErrorMessage = message };

    public static StartInterviewResult Ok(Guid sessionId) =>
        new() { Success = true, SessionId = sessionId };
}
