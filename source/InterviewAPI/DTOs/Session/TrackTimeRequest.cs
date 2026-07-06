namespace InterviewAPI.DTOs.Session;

public class TrackTimeRequest
{
    public Guid SessionQuestionId { get; set; }
    public int ResponseTimeMs { get; set; }
}
