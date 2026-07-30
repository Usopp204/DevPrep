using Microsoft.EntityFrameworkCore;
using QuestionAPI.Data;
using QuestionAPI.Models;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Services;

public class QuestionIndexEligibilityService : IQuestionIndexEligibilityService
{
    private const int RequiredQuestionsPerLevel = 50;

    private static readonly string[] RequiredLevels =
    [
        "Intern",
        "Fresher",
        "Junior",
        "Middle",
        "Senior",
        "Lead"
    ];

    private readonly QuestionBankDbContext _db;
    private readonly IQuestionIndexClient _indexClient;

    public QuestionIndexEligibilityService(
        QuestionBankDbContext db,
        IQuestionIndexClient indexClient)
    {
        _db = db;
        _indexClient = indexClient;
    }

    public async Task<bool> IsReadyAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var categoryIsActive = await _db.QuestionCategories
            .AsNoTracking()
            .AnyAsync(
                category => category.CategoryId == categoryId && category.IsActive,
                cancellationToken);

        if (!categoryIsActive)
        {
            return false;
        }

        var counts = await _db.Questions
            .AsNoTracking()
            .Where(question =>
                question.CategoryId == categoryId &&
                !question.IsDeleted &&
                question.Status == "Active" &&
                question.PositionId != null &&
                question.Position != null &&
                !question.Position.IsDeleted &&
                (question.Difficulty == "Intern" ||
                 question.Difficulty == "Fresher" ||
                 question.Difficulty == "Junior" ||
                 question.Difficulty == "Middle" ||
                 question.Difficulty == "Senior" ||
                 question.Difficulty == "Lead"))
            .GroupBy(question => question.Difficulty)
            .Select(group => new
            {
                Level = group.Key,
                Count = group
                    .Select(question => question.QuestionText.Trim())
                    .Distinct()
                    .Count()
            })
            .ToDictionaryAsync(
                item => item.Level,
                item => item.Count,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

        return RequiredLevels.All(level =>
            counts.GetValueOrDefault(level) >= RequiredQuestionsPerLevel);
    }

    public async Task SynchronizeCategoryAsync(
        Guid categoryId,
        Guid? changedQuestionId,
        CancellationToken cancellationToken)
    {
        var isReady = await IsReadyAsync(categoryId, cancellationToken);
        var questions = await _db.Questions
            .AsNoTracking()
            .Include(question => question.Category)
            .Include(question => question.Position)
            .Where(question => question.CategoryId == categoryId)
            .ToListAsync(cancellationToken);

        var knownIds = questions
            .Select(question => question.QuestionId)
            .Append(changedQuestionId ?? Guid.Empty)
            .Where(questionId => questionId != Guid.Empty)
            .Distinct()
            .ToList();
        var questionsById = questions.ToDictionary(question => question.QuestionId);

        foreach (var questionId in knownIds)
        {
            questionsById.TryGetValue(questionId, out var question);
            var synchronized = isReady && question is not null && IsEligible(question)
                ? await _indexClient.UpsertAsync(
                    question,
                    question.Category?.Category,
                    question.Position?.Name)
                : await _indexClient.DeleteAsync(questionId);

            if (!synchronized)
            {
                throw new InvalidOperationException(
                    $"AI index synchronization failed for category {categoryId}.");
            }
        }
    }

    private static bool IsEligible(Question question)
    {
        return !question.IsDeleted &&
               string.Equals(question.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
               question.Category?.IsActive == true &&
               question.Position is { IsDeleted: false } &&
               RequiredLevels.Contains(question.Difficulty, StringComparer.OrdinalIgnoreCase);
    }
}
