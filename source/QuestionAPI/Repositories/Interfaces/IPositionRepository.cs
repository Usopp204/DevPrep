using QuestionAPI.Models;

namespace QuestionAPI.Repositories.Interfaces;

public interface IPositionRepository
{
    IQueryable<Position> Query();

    Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Position?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tìm theo tên trong các dòng CHƯA xoá (loại trừ excludedId khi update).</summary>
    Task<Position?> FindActiveByNameAsync(string name, Guid? excludedId, CancellationToken cancellationToken);

    /// <summary>Số câu hỏi (chưa xoá) đang gắn position.</summary>
    Task<int> CountQuestionsAsync(Guid positionId, CancellationToken cancellationToken);

    Task AddAsync(Position position, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
