namespace WebClient.Services.Shared;

public class ApiGatewayOptions
{
    // Where WebClient's own server-side HttpClient reaches the gateway. Inside compose that is
    // the internal DNS name http://apigateway:8080/, which no browser can resolve.
    public string? BaseUrl { get; set; }

    // Where a BROWSER reaches the gateway: the published host address. Manager views print this
    // straight into their fetch() calls, so it must never carry a compose DNS name. Left unset
    // it falls back to BaseUrl, which is right outside Docker where the two are the same.
    public string? PublicBaseUrl { get; set; }
}
