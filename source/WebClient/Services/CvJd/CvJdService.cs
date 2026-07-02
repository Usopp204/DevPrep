using WebClient.Models.CvJd;
using WebClient.Services.Shared;

namespace WebClient.Services.CvJd;

public interface ICvJdService
{
    CvJdViewModel GetOverview();
}

public sealed class CvJdService(IApiClient apiClient) : ICvJdService
{
    public CvJdViewModel GetOverview()
    {
        _ = apiClient;

        return new CvJdViewModel
        {
            ModuleName = "CV & JD Processing",
            Epic = "CV & JD Processing",
            Summary = "CV and JD processing placeholder for future API Gateway integration."
        };
    }
}
