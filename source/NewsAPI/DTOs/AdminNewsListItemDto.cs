using System;

namespace NewsAPI.DTOs;

public class AdminNewsListItemDto
{
    public Guid NewsId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Summary { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string Status { get; set; } = null!;

    public int ViewCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}