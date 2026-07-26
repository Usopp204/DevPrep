using QuestionAPI.DTOs.TechStacks;

namespace QuestionAPI.Services.Interfaces;

public interface ITechStackService
{
    Task<TechStackPagedResultDto> GetPageAsync(
        TechStackQueryDto query,
        CancellationToken cancellationToken);

    Task<TechStackDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<TechStackDto> CreateAsync(
        CreateTechStackDto request,
        CancellationToken cancellationToken);

    Task<TechStackDto?> UpdateAsync(
        Guid id,
        UpdateTechStackDto request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
