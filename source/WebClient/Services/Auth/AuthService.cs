using WebClient.Models.Auth;
using WebClient.Services.Shared;

namespace WebClient.Services.Auth;

public interface IAuthService
{
    AuthPageViewModel GetPage(string pageName);
}

public sealed class AuthService(IApiClient apiClient) : IAuthService
{
    public AuthPageViewModel GetPage(string pageName)
    {
        _ = apiClient;

        return new AuthPageViewModel
        {
            ModuleName = "AUTH + PROFILE",
            PageName = pageName,
            Epic = "AUTH + PROFILE",
            Summary = $"{pageName} placeholder for future API Gateway integration."
        };
    }
}
