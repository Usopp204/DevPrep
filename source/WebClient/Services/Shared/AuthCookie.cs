namespace WebClient.Services.Shared;

// Name of the cookie that holds the JWT access token, set by the Login flow. Read server-side
// by JwtDelegatingHandler to authenticate outgoing Gateway calls. This cookie is HttpOnly, so it is
// only readable on the server (client scripts reach the Gateway via same-origin proxy actions).
public static class AuthCookie
{
    public const string Name = "access_token";
}
