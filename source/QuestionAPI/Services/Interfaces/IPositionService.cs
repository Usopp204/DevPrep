using QuestionAPI.DTOs.Positions;

namespace QuestionAPI.Services.Interfaces;

public interface IPositionService
{
    Task<PositionPagedResultDto> GetPageAsync(PositionQueryDto query, CancellationToken cancellationToken);

    Task<PositionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PositionDto> CreateAsync(CreatePositionDto request, CancellationToken cancellationToken);

    /// <summary>Sửa tên/mô tả. Nhờ FK, rename tự phản ánh sang mọi câu hỏi — không cần propagate.</summary>
    Task<PositionDto?> UpdateAsync(Guid id, UpdatePositionDto request, CancellationToken cancellationToken);

    /// <summary>Xoá mềm (IsDeleted). Câu hỏi đang gắn position vẫn giữ FK và hiển thị tên bình thường.</summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Khôi phục position đã xoá mềm (chặn nếu đã có position active trùng tên).</summary>
    Task<PositionDto?> RestoreAsync(Guid id, CancellationToken cancellationToken);
}
