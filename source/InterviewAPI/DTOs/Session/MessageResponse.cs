namespace InterviewAPI.DTOs.Session;

public class MessageResponse
{
    public bool Ended { get; set; }
    public NextQuestionDto? NextQuestion { get; set; }

    // Authoritative progress from the server — the Room's progress display should sync from
    // these instead of counting locally, so a duplicate/retried request can never push the
    // shown number past reality.
    public int AnsweredQuestions { get; set; }
    public int TotalQuestions { get; set; }
}
