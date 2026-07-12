using NewsAPI.DTOs;

namespace NewsAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ xem tin tức cho guest/candidate:
///   - GetListAsync: danh sách tin đã xuất bản, mới nhất trước, phân trang.
///   - GetDetailAsync: chi tiết một bài (tăng lượt xem); null nếu không tồn tại/chưa xuất bản.
/// </summary>
public interface INewsService
{
    Task<PagedResult<NewsListItemDto>> GetListAsync(int page, int pageSize);
    Task<NewsDetailDto?> GetDetailAsync(Guid id);
    Task<NewsDetailDto?> GetDetailBySlugAsync(string slug);
}
