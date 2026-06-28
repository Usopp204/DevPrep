using WebClient.Models.Evaluation;
using WebClient.Services.Shared;

namespace WebClient.Services.Evaluation;

public interface IEvaluationService
{
    EvaluationViewModel GetOverview();
}

public sealed class EvaluationService(IApiClient apiClient) : IEvaluationService
{
    public EvaluationViewModel GetOverview()
    {
        _ = apiClient;

        return new EvaluationViewModel
        {
            ModuleName = "Evaluation & Reporting",
            Epic = "Evaluation & Reporting",
            Summary = "Evaluation and reporting placeholder for future API Gateway integration."
        };
    }
}
