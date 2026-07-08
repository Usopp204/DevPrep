using System.ComponentModel.DataAnnotations;

namespace EvaluationAPI.Models;

public class PerformanceMetric
{
    [Key]
    public Guid MetricId { get; set; }

    public Guid SessionId { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }

    public DateTime RecordedAt { get; set; }
}
