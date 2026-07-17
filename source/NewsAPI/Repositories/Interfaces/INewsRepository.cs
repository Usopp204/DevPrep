using NewsAPI.Models;

namespace NewsAPI.Repositories.Interfaces;

/// <summary>
/// Truy cập dữ liệu tin tức công khai. QueryPublished trả về các bài đã xuất bản
/// (Status='Published') và chưa bị xoá mềm (IsDeleted=0).
/// </summary>
public interface INewsRepository
{
    IQueryable<NewsArticle> QueryPublished();
    Task<NewsArticle?> GetPublishedByIdAsync(Guid id, bool tracking = false);
    Task<NewsArticle?> GetPublishedBySlugAsync(string slug, bool tracking = false);
    Task SaveChangesAsync();
}
