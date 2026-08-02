using Microsoft.Data.SqlClient;
using QuestionAPI.DTOs;

namespace QuestionAPI.Services;

/// <summary>
/// Exports the full question bank for internal consumers (ai-service RAG seeding).
/// Uses raw SQL on purpose: the EF model (Question.cs) does not match the live table schema
/// (Content/Level/ExpectedAnswer vs actual Question/Answer columns), so any EF query would fail
/// with "Invalid column name". Raw SQL reads the real columns directly.
/// Position was normalized into its own table (Migration_2026-07-24_CreatePositionsTable.sql):
/// Questions.Position (text) is gone, replaced by Questions.PositionId (FK to Positions). The
/// JOIN below resolves PositionId back to Positions.Name so this still exports a plain position
/// NAME, not a GUID — ai-service's Chroma metadata and RAG filters are keyed by name, not ID.
///
/// TODO: renaming or soft-deleting a Position in the admin UI does NOT re-index the questions
/// already in Chroma — their "position" metadata keeps the OLD name until someone re-runs
/// build_index.py. A rename silently empties build_question_pool() for that position (no error,
/// just an empty RAG pool). See PositionService.cs (UpdateAsync/SoftDeleteAsync) — no re-index
/// hook exists there yet. Flagged to the team; not fixed here (see BƯỚC 2 report).
/// </summary>
public class InternalQuestionService
{
    private readonly string _connectionString;

    public InternalQuestionService(IConfiguration configuration)
    {
        _connectionString = configuration.GetValue<string>("QUESTION_DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("QUESTION_DB_CONNECTION_STRING is missing in .env.");
    }

    public async Task<List<QuestionExportDto>> ExportAllAsync(CancellationToken ct)
    {
        // p.IsDeleted = 0: a soft-deleted Position is excluded from the RAG pool along with
        // its questions (consistent with the soft-delete convention elsewhere — a deleted
        // Position is treated as no longer offered). Restoring the Position later requires
        // re-running the export/seed for those questions to reappear in Chroma.
        const string sql = @"
            WITH EligibleCategories AS
            (
                SELECT q.CategoryId
                FROM Questions q
                INNER JOIN QuestionCategories c ON q.CategoryId = c.CategoryId
                INNER JOIN Positions p ON q.PositionId = p.PositionId
                WHERE q.IsDeleted = 0
                  AND q.Status = 'Active'
                  AND c.IsActive = 1
                  AND p.IsDeleted = 0
                  AND q.PositionId IS NOT NULL
                GROUP BY q.CategoryId
                HAVING COUNT(DISTINCT CASE WHEN q.Difficulty = 'Intern' THEN LTRIM(RTRIM(q.Question)) END) >= 50
                   AND COUNT(DISTINCT CASE WHEN q.Difficulty = 'Fresher' THEN LTRIM(RTRIM(q.Question)) END) >= 50
                   AND COUNT(DISTINCT CASE WHEN q.Difficulty = 'Junior' THEN LTRIM(RTRIM(q.Question)) END) >= 50
                   AND COUNT(DISTINCT CASE WHEN q.Difficulty = 'Middle' THEN LTRIM(RTRIM(q.Question)) END) >= 50
                   AND COUNT(DISTINCT CASE WHEN q.Difficulty = 'Senior' THEN LTRIM(RTRIM(q.Question)) END) >= 50
                   AND COUNT(DISTINCT CASE WHEN q.Difficulty = 'Lead' THEN LTRIM(RTRIM(q.Question)) END) >= 50
            )
            SELECT q.QuestionId, q.Question, q.Answer, p.Name AS Position, c.Category AS CategoryName, q.Difficulty
            FROM Questions q
            INNER JOIN QuestionCategories c ON q.CategoryId = c.CategoryId
            INNER JOIN Positions p ON q.PositionId = p.PositionId
            INNER JOIN EligibleCategories ec ON q.CategoryId = ec.CategoryId
            WHERE q.IsDeleted = 0
              AND q.Status = 'Active'
              AND q.PositionId IS NOT NULL
              AND p.IsDeleted = 0
              AND c.IsActive = 1
              AND q.Difficulty IN ('Intern', 'Fresher', 'Junior', 'Middle', 'Senior', 'Lead')";

        var result = new List<QuestionExportDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new QuestionExportDto
            {
                QuestionId = reader.GetGuid(0),
                Question = reader.GetString(1),
                Answer = reader.IsDBNull(2) ? null : reader.GetString(2),
                Position = reader.IsDBNull(3) ? null : reader.GetString(3),
                Category = reader.GetString(4),
                Difficulty = reader.GetString(5)
            });
        }

        return result;
    }
}
