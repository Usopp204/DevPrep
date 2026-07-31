namespace InterviewAPI.DTOs.Session;

// Identifies which question the user wants a hint for. The server looks up the question text
// itself (SessionService.HintAsync) — it never trusts client-supplied text, which would let
// anyone with a valid JWT proxy arbitrary prompts through ai-service's Gemini call for free.
public class HintRequest
{
    public Guid SessionQuestionId { get; set; }
}
