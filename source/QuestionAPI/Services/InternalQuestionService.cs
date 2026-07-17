using Microsoft.Data.SqlClient;
using QuestionAPI.DTOs;

namespace QuestionAPI.Services;

/// <summary>
/// Exports the full question bank for internal consumers (ai-service RAG seeding).
/// Uses raw SQL on purpose: the EF model (Question.cs) does not match the live table schema
/// (Content/Level/ExpectedAnswer vs actual Question/Answer/Position columns), so any EF query
/// would fail with "Invalid column name". Raw SQL reads the real columns directly.
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
        const string sql = @"
            SELECT q.Question, q.Answer, q.Position, c.Category AS CategoryName, q.Difficulty
            FROM Questions q
            INNER JOIN QuestionCategories c ON q.CategoryId = c.CategoryId
            WHERE q.IsDeleted = 0 AND q.Position IS NOT NULL";

        var result = new List<QuestionExportDto>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            result.Add(new QuestionExportDto
            {
                Question = reader.GetString(0),
                Answer = reader.IsDBNull(1) ? null : reader.GetString(1),
                Position = reader.IsDBNull(2) ? null : reader.GetString(2),
                Category = reader.GetString(3),
                Difficulty = reader.GetString(4)
            });
        }

        return result;
    }
}
