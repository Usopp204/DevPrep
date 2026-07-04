namespace InterviewAPI.DTOs.Session;

public class InitResponse
{
    public Guid SessionId { get; set; }
    public int TotalQuestions { get; set; }
    public Guid RoomId { get; set; }
    public string FirstQuestion { get; set; } = string.Empty;
}
