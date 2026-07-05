namespace WebClient.Models.Admin;

public class AdminUserApiResponse<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
}

public class AdminUserPagedResult<T>
{
    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public List<T> Items { get; set; } = new();
}

public class AdminUserQueryDto
{
    public string? Keyword { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}

public class AdminUserListItemDto
{
    public Guid UserId { get; set; }

    public Guid AccountId { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; }

    public bool IsEmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int TotalSessionCount { get; set; }

    public int CreditBalance { get; set; }
}

public class AdminUserDetailDto
{
    public Guid UserId { get; set; }

    public Guid AccountId { get; set; }

    public string? FullName { get; set; }

    public string? Email { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; }

    public bool IsEmailVerified { get; set; }

    public bool IsFaceVerified { get; set; }

    public bool IsVoiceVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? BlockedReason { get; set; }

    public DateTime? BlockedAt { get; set; }

    public Guid? BlockedBy { get; set; }

    public int CurrentCreditBalance { get; set; }

    public List<AdminUserSessionDto> Sessions { get; set; } = new();
}

public class AdminUserSessionDto
{
    public Guid SessionId { get; set; }

    public string? Title { get; set; }

    public int? DurationMinutes { get; set; }

    public string? Level { get; set; }

    public string? InteractionMode { get; set; }

    public string? QuestionType { get; set; }

    public string? Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int TotalQuestions { get; set; }

    public int AnsweredQuestions { get; set; }

    public int? AvgResponseTimeMs { get; set; }

    public int CreditCost { get; set; }
}

public class AdminUpdateUserStatusDto
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public class AdminUpdateUserRoleDto
{
    public string Role { get; set; } = string.Empty;
}

public class AdminUserViewModel
{
    public AdminUserPagedResult<AdminUserListItemDto> PagedResult { get; set; } = new();

    public AdminUserQueryDto Query { get; set; } = new();
}