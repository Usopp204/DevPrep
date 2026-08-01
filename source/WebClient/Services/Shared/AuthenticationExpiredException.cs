namespace WebClient.Services.Shared;

public sealed class AuthenticationExpiredException : Exception
{
    public AuthenticationExpiredException()
        : base("Your login session has expired.")
    {
    }
}