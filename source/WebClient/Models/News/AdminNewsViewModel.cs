using Microsoft.AspNetCore.Http;

namespace WebClient.Models.News;

public class NewsApiResponse<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}

public class NewsCategoryDto
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Slug { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AdminNewsQueryDto
{
    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public Guid? CategoryId { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public class AdminNewsListItemDto
{
    public Guid NewsId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ViewCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AdminNewsDetailDto
{
    public Guid NewsId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public Guid AuthorId { get; set; }

    public string Status { get; set; } = string.Empty;

    public int ViewCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class AdminCreateNewsDto
{
    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    // KHÔNG bắt buộc nữa — để trống thì backend tự sinh từ Title
    public string? Slug { get; set; }

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string Status { get; set; } = "Draft";
}

public class AdminUpdateNewsDto
{
    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    // KHÔNG bắt buộc nữa — để trống thì backend tự sinh từ Title
    public string? Slug { get; set; }

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string Status { get; set; } = "Draft";
}

public class AdminNewsViewModel
{
    public PagedResult<AdminNewsListItemDto> PagedNews { get; set; } = new();

    public List<NewsCategoryDto> Categories { get; set; } = new();

    public string? CurrentKeyword { get; set; }

    public string? CurrentStatus { get; set; }

    public Guid? CurrentCategoryId { get; set; }

    public int CurrentPageIndex { get; set; } = 1;

    public int CurrentPageSize { get; set; } = 10;
}

public class AdminNewsFormViewModel
{
    public Guid? NewsId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    // KHÔNG bắt buộc nữa — admin có thể để trống, backend tự sinh từ Title
    public string? Slug { get; set; }

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    public string? ExistingThumbnailUrl { get; set; }

    public IFormFile? ThumbnailFile { get; set; }

    public string Status { get; set; } = "Draft";

    public List<NewsCategoryDto> Categories { get; set; } = new();
}