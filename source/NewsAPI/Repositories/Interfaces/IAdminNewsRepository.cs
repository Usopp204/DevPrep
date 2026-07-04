using System;
using System.Linq;
using System.Threading.Tasks;
using NewsAPI.Models;

namespace NewsAPI.Repositories.Interfaces;

public interface IAdminNewsRepository
{
    IQueryable<NewsArticle> Query();
    Task<NewsArticle?> GetByIdAsync(Guid id);
    Task AddAsync(NewsArticle article);
    Task SaveAsync();
}