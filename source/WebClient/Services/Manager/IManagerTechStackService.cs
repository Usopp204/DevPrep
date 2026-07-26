using WebClient.Models.Manager.TechStacks;

namespace WebClient.Services.Manager;

public enum ManagerTechStackResultStatus
{
    Success,
    ValidationFailure,
    Unauthorized,
    Forbidden,
    UpstreamFailure
}

public sealed record ManagerTechStackServiceResult<T>(
    ManagerTechStackResultStatus Status,
    T? Data = default,
    string? Message = null)
{
    public bool IsSuccess => Status == ManagerTechStackResultStatus.Success;

    public static ManagerTechStackServiceResult<T> Succeeded(T data) =>
        new(ManagerTechStackResultStatus.Success, data);

    public static ManagerTechStackServiceResult<T> Failed(
        ManagerTechStackResultStatus status,
        string message) => new(status, default, message);
}

public interface IManagerTechStackService
{
    Task<ManagerTechStackServiceResult<ManagerTechStackPagedResponse>> GetListAsync(
        int pageIndex,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken = default);
}
