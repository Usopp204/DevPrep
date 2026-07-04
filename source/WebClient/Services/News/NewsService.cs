using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using WebClient.Models.News;
using WebClient.Models.Shared;
using WebClient.Services.Shared;

namespace WebClient.Services.News;

public interface INewsService
{
    Task<NewsViewModel> GetListAsync(int page, int pageSize);
    Task<NewsDetailViewModel> GetDetailAsync(Guid id);
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
                .GetFromJsonAsync<BackendResponse<WebClient.Models.Shared.PagedResult<NewsListItem>>>(url);

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

    public async Task<NewsDetailViewModel> GetDetailAsync(Guid id)
    {
        try
        {
            using var resp = await apiClient.HttpClient.GetAsync($"api/news/{id}");
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return new NewsDetailViewModel { Found = false, Error = "Article not found." };
            }

            var body = await resp.Content.ReadFromJsonAsync<BackendResponse<NewsDetailData>>();
            if (resp.IsSuccessStatusCode && body?.Success == true && body.Data is not null)
            {
                var d = body.Data;
                var vm = new NewsDetailViewModel
                {
                    Found = true,
                    NewsId = d.NewsId,
                    Title = d.Title,
                    Summary = d.Summary,
                    Content = d.Content,
                    ThumbnailUrl = d.ThumbnailUrl,
                    CategoryName = d.CategoryName,
                    ViewCount = d.ViewCount,
                    PublishedAt = d.PublishedAt,
                    CreatedAt = d.CreatedAt
                };

                // Bài liên quan: lấy vài bài mới, loại bài hiện tại (best-effort).
                try
                {
                    var list = await GetListAsync(1, 6);
                    vm.Related = list.Items.Where(i => i.NewsId != id).Take(3).ToList();
                }
                catch { /* related là phụ, lỗi thì bỏ qua */ }

                return vm;
            }

            return new NewsDetailViewModel { Found = false, Error = body?.Message ?? "Article not found." };
        }
        catch (Exception)
        {
            return new NewsDetailViewModel
            {
                Found = false,
                Error = "Cannot reach the server. Please make sure the API Gateway is running and try again."
            };
        }
    }
}
