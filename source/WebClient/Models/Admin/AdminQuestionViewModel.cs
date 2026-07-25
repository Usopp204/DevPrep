namespace WebClient.Models.Admin;

public class QuestionApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

public class AdminPagedResultDto<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}

public class AdminCategoryDto
{
    public Guid CategoryId { get; set; }

    public string Category { get; set; } = string.Empty;
}
public class AdminPositionDto
{
    public Guid PositionId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdminQuestionQueryDto
{
    public string? Keyword { get; set; }

    public string? Difficulty { get; set; }

    // public string? TechStack { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Status { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public class AdminQuestionListItemDto
{
    public Guid QuestionId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Category { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    //public string? TechStack { get; set; }

    //public string? Tags { get; set; }

    public string? Position { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class AdminQuestionDetailDto
{
    public Guid QuestionId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Category { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string? Answer { get; set; }

    // public string? TechStack { get; set; }

    // public string? Tags { get; set; }

     public string? Position { get; set; }
    public Guid? PositionId { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public class AdminCreateQuestionDto
{
    public Guid? CategoryId { get; set; }

    public string Question { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    // public string TechStack { get; set; } = string.Empty;

    // public string Tags { get; set; } = string.Empty;

    //public string Position { get; set; } = string.Empty;
    public Guid? PositionId { get; set; }
    public string Status { get; set; } = "Active";
}

public class AdminUpdateQuestionDto
{
    public Guid? CategoryId { get; set; }

    public string? Question { get; set; }

    public string? Difficulty { get; set; }

    public string? Answer { get; set; }

    // public string? TechStack { get; set; }

    //public string? Tags { get; set; }

    //public string? Position { get; set; }
    public Guid? PositionId { get; set; }

    public string? Status { get; set; }
}

public class AdminQuestionViewModel
{
    public AdminPagedResultDto<AdminQuestionListItemDto> PagedResult { get; set; } = new();

    public List<AdminCategoryDto> Categories { get; set; } = new();
    public List<AdminPositionDto> Positions { get; set; } = new();   // ← THÊM
}