using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.News;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.News;

public interface INewsService
{
    Task<NewsViewModel> GetListAsync(int page, int pageSize);
}

public sealed class NewsService(IApiClient apiClient) : INewsService
{
    public async Task<NewsViewModel> GetListAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;

        var url = QueryHelpers.AddQueryString("api/news", new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        });

        try
        {
            var body = await apiClient.HttpClient
                .GetFromJsonAsync<BackendResponse<PagedResult<NewsListItem>>>(url);

            if (body?.Success == true && body.Data is not null)
            {
                return new NewsViewModel
                {
                    Items = body.Data.Items ?? new List<NewsListItem>(),
                    Page = body.Data.PageIndex <= 0 ? page : body.Data.PageIndex,
                    PageSize = body.Data.PageSize <= 0 ? pageSize : body.Data.PageSize,
                    TotalPages = Math.Max(1, body.Data.TotalPages)
                };
            }

            return new NewsViewModel
            {
                Page = page,
                PageSize = pageSize,
                Error = body?.Message ?? "Could not load news."
            };
        }
        catch (Exception)
        {
            return new NewsViewModel
            {
                Page = page,
                PageSize = pageSize,
                Error = "Cannot reach the server. Please make sure the API Gateway is running and try again."
            };
        }
    }
}
