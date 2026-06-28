using WebClient.Models.Manager;
using WebClient.Services.Shared;

namespace WebClient.Services.Manager;

public interface IManagerService
{
    ManagerViewModel GetOverview();
}

public sealed class ManagerService(IApiClient apiClient) : IManagerService
{
    public ManagerViewModel GetOverview()
    {
        _ = apiClient;

        return new ManagerViewModel
        {
            ModuleName = "Manager",
            Epic = "Manager",
            Summary = "Manager read-only placeholder for future API Gateway integration."
        };
    }
}
