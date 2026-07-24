namespace WebClient.Models.Shared;

public class ApiCallResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public static ApiCallResult<T> Ok(T? data, int statusCode) => new()
    {
        Success = true,
        Data = data,
        StatusCode = statusCode
    };

    public static ApiCallResult<T> Fail(string error, int statusCode) => new()
    {
        Success = false,
        Error = error,
        StatusCode = statusCode
    };
}