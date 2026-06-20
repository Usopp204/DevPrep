using Microsoft.EntityFrameworkCore;
using NewsAPI.Models;
using NewsAPI.Repositories.Interfaces;

namespace NewsAPI.Repositories;

public class NewsRepository : INewsRepository
{
    private const string PublishedStatus = "Published";

    private readonly NewsDbContext _db;

    public NewsRepository(NewsDbContext db)
    {
        _db = db;
    }

    // Tin công khai: đã xuất bản + chưa xoá mềm.
    public IQueryable<NewsArticle> QueryPublished() =>
        _db.NewsArticles.AsNoTracking().Where(n => n.Status == PublishedStatus && !n.IsDeleted);

    public Task<NewsArticle?> GetPublishedByIdAsync(Guid id, bool tracking = false)
    {
        IQueryable<NewsArticle> query = _db.NewsArticles.Include(n => n.Category);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(n => n.NewsId == id && n.Status == PublishedStatus && !n.IsDeleted);
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
