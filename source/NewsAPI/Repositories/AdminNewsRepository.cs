using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NewsAPI.Models;
using NewsAPI.Repositories.Interfaces;

namespace NewsAPI.Repositories;

public class AdminNewsRepository : IAdminNewsRepository
{
    private readonly NewsDbContext _context;

    public AdminNewsRepository(NewsDbContext context)
    {
        _context = context;
    }

    // Lấy toàn bộ danh sách (gồm cả bài viết đã xóa mềm để Admin quản lý)
    public IQueryable<NewsArticle> Query() =>
        _context.NewsArticles;

    public async Task<NewsArticle?> GetByIdAsync(Guid id) =>
        await _context.NewsArticles.FirstOrDefaultAsync(x => x.NewsId == id);

    public async Task AddAsync(NewsArticle article) =>
        await _context.NewsArticles.AddAsync(article);

    public async Task SaveAsync() =>
        await _context.SaveChangesAsync();
}