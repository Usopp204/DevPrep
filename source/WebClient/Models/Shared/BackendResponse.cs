namespace WebClient.Models.Shared;

/// <summary>
/// Bao khớp với ApiResponse&lt;T&gt; của các service backend (success/message/data).
/// Dùng để giải mã phản hồi JSON từ API Gateway (System.Text.Json khớp tên không phân biệt hoa thường).
/// </summary>
public class BackendResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}
