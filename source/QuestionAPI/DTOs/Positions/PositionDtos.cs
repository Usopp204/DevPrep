using System.ComponentModel.DataAnnotations;

namespace QuestionAPI.DTOs.Positions;

/// <summary>Filter trạng thái cho danh sách Position: All | Active (chưa xoá) | Deleted (đã xoá mềm).</summary>
public enum PositionStatusFilter
{
    All,
    Active,
    Deleted
}

public class PositionQueryDto
{
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public string? Search { get; set; }

    public PositionStatusFilter Status { get; set; } = PositionStatusFilter.Active;
}

public class CreatePositionDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
}

public class UpdatePositionDto
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
}

public class PositionDto
{
    public Guid PositionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Số câu hỏi (chưa xoá) đang gắn position này — để admin cân nhắc trước khi xoá.</summary>
    public int QuestionCount { get; set; }
}

public class PositionPagedResultDto
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyList<PositionDto> Items { get; set; } = Array.Empty<PositionDto>();
}
