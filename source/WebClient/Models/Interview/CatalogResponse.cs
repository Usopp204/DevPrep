namespace WebClient.Models.Interview;

// Mirrors InterviewAPI.DTOs.AiService.AiCatalogResponse (roles + technologies per role).
public class CatalogResponse
{
    public List<string> Roles { get; set; } = new();
    public Dictionary<string, List<string>> Technologies { get; set; } = new();
}
