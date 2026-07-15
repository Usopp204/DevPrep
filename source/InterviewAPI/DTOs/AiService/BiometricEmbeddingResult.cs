using System.Text.Json.Serialization;

namespace InterviewAPI.DTOs.AiService;

// Mirrors ai-service POST /internal/biometrics/{face|voice}/embed JSON
// (snake_case field names). Quality is face-only; null for voice.
public record BiometricEmbeddingResult(
    [property: JsonPropertyName("embedding")] float[] Embedding,
    [property: JsonPropertyName("dim")] int Dim,
    [property: JsonPropertyName("quality")] double? Quality,
    [property: JsonPropertyName("model_version")] string ModelVersion);
