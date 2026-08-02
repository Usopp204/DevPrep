using NewsAPI.DTOs;

namespace NewsAPI.Services.Interfaces;

/// <summary>
/// Nghiệp vụ xem tin tức cho guest/candidate:
///   - GetListAsync: danh sách tin đã xuất bản, lọc theo từ khoá/danh mục (tuỳ chọn), mới nhất trước, phân trang.
///   - GetCategoriesAsync: các danh mục đang có ít nhất một bài đã xuất bản (dùng cho chip lọc).
///   - GetDetailAsync: chi tiết một bài (tăng lượt xem); null nếu không tồn tại/chưa xuất bản.
/// </summary>
public interface INewsService
{
    Task<PagedResult<NewsListItemDto>> GetListAsync(NewsQueryDto query);
    Task<List<NewsCategoryDto>> GetCategoriesAsync();
    Task<NewsDetailDto?> GetDetailAsync(Guid id);
    Task<NewsDetailDto?> GetDetailBySlugAsync(string slug);
}
