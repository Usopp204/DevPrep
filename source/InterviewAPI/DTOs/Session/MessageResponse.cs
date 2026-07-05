namespace InterviewAPI.DTOs.Session;

public class MessageResponse
{
    public bool Ended { get; set; }
    public NextQuestionDto? NextQuestion { get; set; }
}
