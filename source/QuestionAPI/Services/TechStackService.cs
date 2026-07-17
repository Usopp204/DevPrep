using Microsoft.EntityFrameworkCore;
using QuestionAPI.DTOs.TechStacks;
using QuestionAPI.Exceptions;
using QuestionAPI.Helpers;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Services;

public class TechStackService : ITechStackService
{
    private const int MaximumNameLength = 200;
    private const int MaximumDescriptionLength = 500;

    private readonly ITechStackRepository _repository;

    public TechStackService(ITechStackRepository repository)
    {
        _repository = repository;
    }

    public async Task<TechStackPagedResultDto> GetPageAsync(
        TechStackQueryDto query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var normalizedQuery = new TechStackQueryDto
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            Search = NormalizeOptional(query.Search),
            Status = query.Status
        };

        var page = await _repository.GetPageAsync(normalizedQuery, cancellationToken);

        return new TechStackPagedResultDto
        {
            PageIndex = normalizedQuery.PageIndex,
            PageSize = normalizedQuery.PageSize,
            TotalRecords = page.TotalRecords,
            TotalPages = page.TotalRecords == 0
                ? 0
                : (int)Math.Ceiling((double)page.TotalRecords / normalizedQuery.PageSize),
            Items = page.Items
        };
    }

    public async Task<TechStackDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        return category is null ? null : Map(category);
    }

    public async Task<TechStackDto> CreateAsync(
        CreateTechStackDto request,
        CancellationToken cancellationToken)
    {
        var input = NormalizeInput(request.Name, request.Description);
        await EnsureNameIsUniqueAsync(input.Name, null, cancellationToken);

        var category = new QuestionCategory
        {
            Category = input.Name,
            Description = input.Description,
            IsActive = true
        };

        await _repository.AddAsync(category, cancellationToken);
        await SaveWithDuplicateProtectionAsync(cancellationToken);

        return Map(category);
    }

    public async Task<TechStackDto?> UpdateAsync(
        Guid id,
        UpdateTechStackDto request,
        CancellationToken cancellationToken)
    {
        if (!request.IsActive.HasValue)
        {
            throw new TechStackValidationException("IsActive is required.");
        }

        var input = NormalizeInput(request.Name, request.Description);
        var category = await _repository.GetTrackedByIdAsync(id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        await EnsureNameIsUniqueAsync(input.Name, id, cancellationToken);

        category.Category = input.Name;
        category.Description = input.Description;
        category.IsActive = request.IsActive.Value;

        await SaveWithDuplicateProtectionAsync(cancellationToken);
        return Map(category);
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await _repository.GetTrackedByIdAsync(id, cancellationToken);

        if (category is null)
        {
            return false;
        }

        if (!category.IsActive)
        {
            return true;
        }

        category.IsActive = false;
        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureNameIsUniqueAsync(
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicate = await _repository.FindByNameAsync(name, excludedId, cancellationToken);

        if (duplicate is null)
        {
            return;
        }

        throw CreateDuplicateException(duplicate.IsActive);
    }

    private async Task SaveWithDuplicateProtectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (SqlServerUniqueViolationClassifier.IsUniqueViolation(exception))
        {
            throw CreateDuplicateException(existingIsActive: true);
        }
    }

    private static NormalizedTechStackInput NormalizeInput(string? name, string? description)
    {
        var normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new TechStackValidationException("Name is required.");
        }

        if (normalizedName.Length > MaximumNameLength)
        {
            throw new TechStackValidationException(
                $"Name must not exceed {MaximumNameLength} characters.");
        }

        var normalizedDescription = NormalizeOptional(description);
        if (normalizedDescription?.Length > MaximumDescriptionLength)
        {
            throw new TechStackValidationException(
                $"Description must not exceed {MaximumDescriptionLength} characters.");
        }

        return new NormalizedTechStackInput(normalizedName, normalizedDescription);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateQuery(TechStackQueryDto query)
    {
        if (query.PageIndex < 1)
        {
            throw new TechStackValidationException("PageIndex must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new TechStackValidationException("PageSize must be between 1 and 100.");
        }

        if (!Enum.IsDefined(query.Status))
        {
            throw new TechStackValidationException("Status must be All, Active, or Inactive.");
        }
    }

    private static TechStackDuplicateException CreateDuplicateException(bool existingIsActive)
    {
        var message = existingIsActive
            ? "A Tech Stack with this name already exists."
            : "A deactivated Tech Stack with this name already exists. Reactivate the existing record instead.";

        return new TechStackDuplicateException(message);
    }

    private static TechStackDto Map(QuestionCategory category)
    {
        return new TechStackDto
        {
            TechStackId = category.CategoryId,
            Name = category.Category,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };
    }

    private sealed record NormalizedTechStackInput(string Name, string? Description);
}
