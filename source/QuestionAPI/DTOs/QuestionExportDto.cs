namespace QuestionAPI.DTOs;

/// <summary>
/// Raw question record served to internal consumers (ai-service seeds its RAG index from this).
/// Category is the joined category NAME (text), not the CategoryId.
/// Values are returned exactly as stored in the DB — no casing/format transformation here.
/// </summary>
public class QuestionExportDto
{
    public Guid QuestionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public string? Position { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
}
