namespace WebClient.Models.Interview;

// The question the Room asks a hint for (same-origin proxy to the Gateway).
public class HintProxyRequest
{
    public string Question { get; set; } = string.Empty;
}
