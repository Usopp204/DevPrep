using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NewsAPI.Models;

[Index("CategoryId", Name = "IX_News_Category")]
[Index("PublishedAt", Name = "IX_News_Published")]
[Index("Status", "IsDeleted", Name = "IX_News_Status")]
[Index("Slug", Name = "UQ_NewsArticles_Slug", IsUnique = true)]
public partial class NewsArticle
{
    [Key]
    public Guid NewsId { get; set; }

    public Guid? CategoryId { get; set; }

    [StringLength(255)]
    public string Title { get; set; } = null!;

    [StringLength(280)]
    public string Slug { get; set; } = null!;

    [StringLength(500)]
    public string? Summary { get; set; }

    public string Content { get; set; } = null!;

    [StringLength(1024)]
    public string? ThumbnailUrl { get; set; }

    public Guid AuthorId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public int ViewCount { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("NewsArticles")]
    public virtual NewsCategory? Category { get; set; }
}
