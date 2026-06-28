using WebClient.Models.News;
using WebClient.Services.Shared;

namespace WebClient.Services.News;

public interface INewsService
{
    NewsViewModel GetOverview();
}

public sealed class NewsService(IApiClient apiClient) : INewsService
{
    public NewsViewModel GetOverview()
    {
        _ = apiClient;

        return new NewsViewModel
        {
            ModuleName = "News",
            Epic = "News",
            Summary = "News placeholder for future API Gateway integration."
        };
    }
}
