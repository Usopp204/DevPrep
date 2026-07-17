using WebClient.Models.Admin.TechStacks;

namespace WebClient.Services.Admin;

public enum AdminTechStackResultStatus
{
    Success,
    ValidationFailure,
    Unauthorized,
    Forbidden,
    NotFound,
    DuplicateConflict,
    UpstreamFailure
}

public sealed record AdminTechStackServiceResult<T>(
    AdminTechStackResultStatus Status,
    T? Data = default,
    string? Message = null)
{
    public bool IsSuccess => Status == AdminTechStackResultStatus.Success;

    public static AdminTechStackServiceResult<T> Succeeded(T? data, string? message = null) =>
        new(AdminTechStackResultStatus.Success, data, message);

    public static AdminTechStackServiceResult<T> Failed(
        AdminTechStackResultStatus status,
        string? message = null) => new(status, default, message);
}

public interface IAdminTechStackService
{
    Task<AdminTechStackServiceResult<AdminTechStackPagedResponse>> GetListAsync(
        int pageIndex,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken = default);

    Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> GetByIdAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default);

    Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> CreateAsync(
        CreateTechStackViewModel model,
        CancellationToken cancellationToken = default);

    Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> UpdateAsync(
        Guid techStackId,
        UpdateTechStackViewModel model,
        CancellationToken cancellationToken = default);

    Task<AdminTechStackServiceResult<object?>> DeactivateAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default);

    Task<AdminTechStackServiceResult<AdminTechStackItemViewModel>> ReactivateAsync(
        Guid techStackId,
        CancellationToken cancellationToken = default);
}
