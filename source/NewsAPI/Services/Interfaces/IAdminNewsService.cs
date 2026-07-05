using System;
using System.Threading.Tasks;
using NewsAPI.DTOs;
using NewsAPI.Models;

namespace NewsAPI.Services.Interfaces;

public interface IAdminNewsService
{
    Task<PagedResult<AdminNewsListItemDto>> GetListAsync(AdminNewsQueryDto q);
    Task<AdminNewsDetailDto?> GetDetailAsync(Guid id);
    Task<AdminNewsDetailDto> CreateAsync(AdminCreateNewsDto dto, Guid authorId);
    Task<AdminNewsDetailDto?> UpdateAsync(Guid id, AdminUpdateNewsDto dto);
    Task<bool> DeleteAsync(Guid id, Guid adminId);
    Task<PagedResult<AdminNewsListItemDto>> FilterAsync(AdminNewsQueryDto q);
    Task<bool> RestoreAsync(Guid id);
    Task<List<NewsCategory>> GetAllCategoriesAsync();
}