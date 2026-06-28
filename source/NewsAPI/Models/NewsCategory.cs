using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NewsAPI.Models;

[Index("Slug", Name = "UQ_NewsCategories_Slug", IsUnique = true)]
public partial class NewsCategory
{
    [Key]
    public Guid CategoryId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(120)]
    public string Slug { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<NewsArticle> NewsArticles { get; set; } = new List<NewsArticle>();
}
