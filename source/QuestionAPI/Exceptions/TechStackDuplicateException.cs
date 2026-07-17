namespace QuestionAPI.Exceptions;

public class TechStackDuplicateException : Exception
{
    public TechStackDuplicateException(string message) : base(message)
    {
    }
}
