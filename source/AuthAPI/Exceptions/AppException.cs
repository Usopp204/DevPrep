namespace AuthAPI.Exceptions;

/// <summary>
/// Exception nghiệp vụ có gắn HTTP status code, được middleware dịch thành response.
/// </summary>
public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }

    protected AppException(string message) : base(message)
    {
    }
}

/// <summary>
/// 400 - Yêu cầu không hợp lệ (ví dụ: OTP sai hoặc đã hết hạn).
/// </summary>
public class BadRequestException : AppException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;

    public BadRequestException(string message) : base(message)
    {
    }
}

/// <summary>
/// 401 - Chưa xác thực / sai thông tin đăng nhập.
/// </summary>
public class UnauthorizedException : AppException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;

    public UnauthorizedException(string message) : base(message)
    {
    }
}

/// <summary>
/// 403 - Đã xác thực nhưng không đủ quyền (ví dụ: chưa verify email, không phải admin).
/// </summary>
public class ForbiddenException : AppException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;

    public ForbiddenException(string message) : base(message)
    {
    }
}

/// <summary>
/// 404 - Không tìm thấy tài nguyên (ví dụ: tài khoản không tồn tại).
/// </summary>
public class NotFoundException : AppException
{
    public override int StatusCode => StatusCodes.Status404NotFound;

    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// 409 - Dữ liệu xung đột (ví dụ: email đã tồn tại).
/// </summary>
public class ConflictException : AppException
{
    public override int StatusCode => StatusCodes.Status409Conflict;

    public ConflictException(string message) : base(message)
    {
    }
}
