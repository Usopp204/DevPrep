using WebClient.Models.Admin;
using WebClient.Services.Shared;

namespace WebClient.Services.Admin;

public interface IAdminService
{
    AdminViewModel GetOverview();
}

public sealed class AdminService(IApiClient apiClient) : IAdminService
{
    public AdminViewModel GetOverview()
    {
        _ = apiClient;

        return new AdminViewModel
        {
            ModuleName = "Admin",
            Epic = "Admin",
            Summary = "Admin placeholder for future API Gateway integration."
        };
    }
}
