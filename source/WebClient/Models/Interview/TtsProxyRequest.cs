namespace WebClient.Models.Interview;

// Text the Room asks the server to synthesize to speech (same-origin proxy to the Gateway).
public class TtsProxyRequest
{
    public string Text { get; set; } = string.Empty;
}
