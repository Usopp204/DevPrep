namespace StatisticsAPI.Models;

/// <summary>
/// Bộ đếm "con số tổng hiện tại" của từng chỉ số (1 dòng / chỉ số).
/// Đây là read-model (cache) — nguồn sự thật vẫn ở DB từng service; bảng này chỉ chép lại tổng để đọc nhanh.
/// </summary>
public class MetricCounter
{
    public string MetricKey { get; set; } = string.Empty;   // vd 'TotalUsers', 'RevenueThisMonth'
    public decimal MetricValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}
