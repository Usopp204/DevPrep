namespace StatisticsAPI.Models;

/// <summary>
/// Ảnh chụp số liệu theo TỪNG NGÀY (1 dòng / ngày, unique theo SnapshotDate) — phục vụ biểu đồ xu hướng.
/// (Đã bỏ 2 cột CvUploaded + AvgSessionScore ngày 2026-07-17 vì nguồn CvJd/Evaluation chưa triển khai.)
/// </summary>
public class StatisticsSnapshot
{
    public Guid SnapshotId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InterviewSessions { get; set; }
    public int CompletedSessions { get; set; }
    public decimal Revenue { get; set; }
    public int PaidOrders { get; set; }
    public int NewFeedbacks { get; set; }
    public DateTime CreatedAt { get; set; }
}
