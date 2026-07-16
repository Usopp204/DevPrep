namespace AuthAPI.DTOs;

// 1. DTO cập nhật trạng thái hoạt động (Block/Unblock)
public class AdminUpdateUserStatusDto
{
    public string Status { get; set; } = null!;
    public string? Reason { get; set; }
}

// 2. DTO cập nhật vai trò hệ thống (Role)
public class AdminUpdateUserRoleDto
{
    public string Role { get; set; } = null!;
}

// 3. DTO chứa thông tin chi tiết Account
public class InternalAccountDto
{
    public Guid AccountId { get; set; }
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsEmailVerified { get; set; }
    public bool IsFaceVerified { get; set; }
    public bool IsVoiceVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// 4. Lớp phân trang 
public class PagedResult<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<T> Items { get; set; } = new();
}