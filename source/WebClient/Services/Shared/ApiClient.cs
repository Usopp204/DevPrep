namespace WebClient.Services.Shared;

public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public HttpClient HttpClient { get; } = httpClient;
}
