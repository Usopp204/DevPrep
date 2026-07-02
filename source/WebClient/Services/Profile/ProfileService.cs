using WebClient.Models.Profile;
using WebClient.Services.Shared;

namespace WebClient.Services.Profile;

public interface IProfileService
{
    ProfileViewModel GetOverview();
}

public sealed class ProfileService(IApiClient apiClient) : IProfileService
{
    public ProfileViewModel GetOverview()
    {
        _ = apiClient;

        return new ProfileViewModel
        {
            ModuleName = "Profile",
            Epic = "AUTH + PROFILE",
            Summary = "Profile placeholder for future API Gateway integration."
        };
    }
}
