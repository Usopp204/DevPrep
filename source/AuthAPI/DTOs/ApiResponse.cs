namespace AuthAPI.DTOs;

/// <summary>
/// Định dạng response chung cho toàn bộ API để client xử lý nhất quán.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };
}
