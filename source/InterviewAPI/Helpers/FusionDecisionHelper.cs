using System.Text.Json;

namespace InterviewAPI.Helpers;

public record FusionWeights(double Bias, double WFace, double WVoice, double DecisionThreshold);

/// <summary>
/// Reads the "fusion" section of ai-service's models/calibration.json (produced by
/// Milestone 3's calibrate.py) and computes the fused pass/fail decision. Returns null
/// wherever real calibration data doesn't exist yet — callers must fall back to
/// requiring both modalities to independently pass their thresholds and liveness checks.
/// </summary>
public static class FusionDecisionHelper
{
    public static FusionWeights? TryLoadFusionWeights(string? calibrationJsonPath)
    {
        if (string.IsNullOrWhiteSpace(calibrationJsonPath) || !File.Exists(calibrationJsonPath))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(calibrationJsonPath));
            if (!doc.RootElement.TryGetProperty("fusion", out var fusion))
                return null;
            return new FusionWeights(
                fusion.GetProperty("bias").GetDouble(),
                fusion.GetProperty("w_face").GetDouble(),
                fusion.GetProperty("w_voice").GetDouble(),
                fusion.GetProperty("decision_threshold").GetDouble());
        }
        catch (Exception)
        {
            // Malformed/partial calibration.json (e.g. being rewritten concurrently) —
            // treat as "not available" rather than throwing during a verification call.
            return null;
        }
    }

    public static bool Decide(FusionWeights weights, double faceScore, double voiceScore)
    {
        var z = weights.Bias + weights.WFace * faceScore + weights.WVoice * voiceScore;
        var sigmoid = 1.0 / (1.0 + Math.Exp(-z));
        return sigmoid >= weights.DecisionThreshold;
    }
}
