namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.Session.MessageResponse.
public class MessageResponse
{
    public bool Ended { get; set; }
    public NextQuestionDto? NextQuestion { get; set; }
    public int AnsweredQuestions { get; set; }
    public int TotalQuestions { get; set; }
}
