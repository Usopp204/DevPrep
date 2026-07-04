using System.Net.Http.Headers;

namespace WebClient.Services.Shared;

// Attaches the JWT (stored in the access_token cookie by the Login flow) as a Bearer header on
// every outgoing Gateway request, so server-side API calls are authenticated.
public sealed class AuthTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = httpContextAccessor.HttpContext?.Request.Cookies[AuthCookie.Name];
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
