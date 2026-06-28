using WebClient.Models.Interview;
using WebClient.Services.Shared;

namespace WebClient.Services.Interview;

public interface IInterviewService
{
    InterviewViewModel GetOverview();
}

public sealed class InterviewService(IApiClient apiClient) : IInterviewService
{
    public InterviewViewModel GetOverview()
    {
        _ = apiClient;

        return new InterviewViewModel
        {
            ModuleName = "Real-time Mock Interview",
            Epic = "Real-time Mock Interview",
            Summary = "Interview placeholder for future API Gateway integration."
        };
    }
}
