namespace QuestionAPI.Exceptions;

/// <summary>Dữ liệu Position không hợp lệ (controller map thành 400).</summary>
public class PositionValidationException : Exception
{
    public PositionValidationException(string message) : base(message)
    {
    }
}

/// <summary>Trùng tên Position (controller map thành 409).</summary>
public class PositionDuplicateException : Exception
{
    public PositionDuplicateException(string message) : base(message)
    {
    }
}
