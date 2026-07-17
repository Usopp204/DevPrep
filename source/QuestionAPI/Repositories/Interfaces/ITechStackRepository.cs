using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Models;

namespace QuestionAPI.Repositories.Interfaces;

public sealed record TechStackPageData(
    IReadOnlyList<TechStackListItemDto> Items,
    int TotalRecords);

public interface ITechStackRepository
{
    Task<TechStackPageData> GetPageAsync(
        TechStackQueryDto query,
        CancellationToken cancellationToken);

    Task<QuestionCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<QuestionCategory?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<QuestionCategory?> FindByNameAsync(
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task AddAsync(QuestionCategory category, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
