namespace EvaluationAPI.Clients;

public class InterviewApiClient : IInterviewApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InterviewApiClient> _logger;

    public InterviewApiClient(HttpClient httpClient, ILogger<InterviewApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> OwnsSessionAsync(Guid sessionId, string? bearerToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"api/interview/sessions/{sessionId}/owns");
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.TryAddWithoutValidation("Authorization", bearerToken);

            using var response = await _httpClient.SendAsync(request, ct);
            // 200 = owns; 404 (not owner / not found) / 401 / 403 = does not own.
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // On a transient failure, deny access (fail closed) rather than leak data.
            _logger.LogWarning(ex,
                "Ownership check against InterviewAPI failed for session {SessionId}; denying access.",
                sessionId);
            return false;
        }
    }
}
