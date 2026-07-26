using Microsoft.EntityFrameworkCore;
using QuestionAPI.DTOs.Positions;
using QuestionAPI.Exceptions;
using QuestionAPI.Helpers;
using QuestionAPI.Models;
using QuestionAPI.Repositories.Interfaces;
using QuestionAPI.Services.Interfaces;

namespace QuestionAPI.Services;

public class PositionService : IPositionService
{
    private const int MaximumNameLength = 50;

    private readonly IPositionRepository _repository;

    public PositionService(IPositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PositionPagedResultDto> GetPageAsync(PositionQueryDto query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);

        var q = _repository.Query().AsNoTracking();

        q = query.Status switch
        {
            PositionStatusFilter.Active => q.Where(p => !p.IsDeleted),
            PositionStatusFilter.Deleted => q.Where(p => p.IsDeleted),
            _ => q
        };

        var search = NormalizeOptional(query.Search);
        if (search is not null)
        {
            var pattern = $"%{search}%";
            q = q.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        var totalRecords = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderBy(p => p.Name)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new PositionDto
            {
                PositionId = p.PositionId,
                Name = p.Name,
                IsDeleted = p.IsDeleted,
                DeletedAt = p.DeletedAt,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                QuestionCount = p.Questions.Count(x => !x.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PositionPagedResultDto
        {
            PageIndex = query.PageIndex,
            PageSize = query.PageSize,
            TotalRecords = totalRecords,
            TotalPages = totalRecords == 0
                ? 0
                : (int)Math.Ceiling((double)totalRecords / query.PageSize),
            Items = items
        };
    }

    public async Task<PositionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var position = await _repository.GetByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return null;
        }

        var questionCount = await _repository.CountQuestionsAsync(id, cancellationToken);
        return Map(position, questionCount);
    }

    public async Task<PositionDto> CreateAsync(CreatePositionDto request, CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);
        await EnsureNameIsUniqueAsync(name, null, cancellationToken);

        var now = VietnamTime.Now;
        var position = new Position
        {
            PositionId = Guid.NewGuid(),
            Name = name,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(position, cancellationToken);
        await SaveWithDuplicateProtectionAsync(cancellationToken);

        return Map(position, 0);
    }

    public async Task<PositionDto?> UpdateAsync(Guid id, UpdatePositionDto request, CancellationToken cancellationToken)
    {
        var name = NormalizeName(request.Name);

        var position = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return null;
        }

        if (position.IsDeleted)
        {
            throw new PositionValidationException(
                "This Position has been deleted. Restore it before updating.");
        }

        await EnsureNameIsUniqueAsync(name, id, cancellationToken);

        position.Name = name;
        position.UpdatedAt = VietnamTime.Now;

        await SaveWithDuplicateProtectionAsync(cancellationToken);

        var questionCount = await _repository.CountQuestionsAsync(id, cancellationToken);
        return Map(position, questionCount);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var position = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return false;
        }

        if (position.IsDeleted)
        {
            return true; // idempotent
        }

        var now = VietnamTime.Now;
        position.IsDeleted = true;
        position.DeletedAt = now;
        position.UpdatedAt = now;

        await _repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PositionDto?> RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var position = await _repository.GetTrackedByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return null;
        }

        if (position.IsDeleted)
        {
            // Chặn khôi phục khi đã có position active trùng tên (unique chỉ áp cho dòng chưa xoá).
            var duplicate = await _repository.FindActiveByNameAsync(position.Name, id, cancellationToken);
            if (duplicate is not null)
            {
                throw new PositionDuplicateException(
                    "An active Position with this name already exists. Rename or delete it before restoring.");
            }

            position.IsDeleted = false;
            position.DeletedAt = null;
            position.UpdatedAt = VietnamTime.Now;
            await SaveWithDuplicateProtectionAsync(cancellationToken);
        }

        var questionCount = await _repository.CountQuestionsAsync(id, cancellationToken);
        return Map(position, questionCount);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        var duplicate = await _repository.FindActiveByNameAsync(name, excludedId, cancellationToken);
        if (duplicate is not null)
        {
            throw new PositionDuplicateException("A Position with this name already exists.");
        }
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
            throw new PositionDuplicateException("A Position with this name already exists.");
        }
    }

    private static string NormalizeName(string? name)
    {
        var normalized = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new PositionValidationException("Name is required.");
        }

        if (normalized.Length > MaximumNameLength)
        {
            throw new PositionValidationException(
                $"Name must not exceed {MaximumNameLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void ValidateQuery(PositionQueryDto query)
    {
        if (query.PageIndex < 1)
        {
            throw new PositionValidationException("PageIndex must be at least 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            throw new PositionValidationException("PageSize must be between 1 and 100.");
        }

        if (!Enum.IsDefined(query.Status))
        {
            throw new PositionValidationException("Status must be All, Active, or Deleted.");
        }
    }

    private static PositionDto Map(Position position, int questionCount)
    {
        return new PositionDto
        {
            PositionId = position.PositionId,
            Name = position.Name,
            IsDeleted = position.IsDeleted,
            DeletedAt = position.DeletedAt,
            CreatedAt = position.CreatedAt,
            UpdatedAt = position.UpdatedAt,
            QuestionCount = questionCount
        };
    }
}
