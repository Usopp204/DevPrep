namespace QuestionAPI.Exceptions;

public class TechStackValidationException : Exception
{
    public TechStackValidationException(string message) : base(message)
    {
    }
}
