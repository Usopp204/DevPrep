namespace WebClient.Models.Shared;

// Mirrors the backend response envelope (Success / Message / Data) returned by every
// InterviewAPI and EvaluationAPI endpoint, so responses deserialize directly.
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
}
