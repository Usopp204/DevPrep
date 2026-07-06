using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Response of ai-service GET /catalog: roles and the technologies available per role.
public class AiCatalogResponse
{
    [JsonPropertyName("roles")]
    public List<string> Roles { get; set; } = new();

    [JsonPropertyName("technologies")]
    public Dictionary<string, List<string>> Technologies { get; set; } = new();
}
