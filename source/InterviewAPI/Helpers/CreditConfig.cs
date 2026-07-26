namespace InterviewAPI.Helpers;

/// <summary>
/// Centralized credit pricing configuration. Change here affects all credit-related logic
/// (deduction, validation, UI messages). Pricing: 2 credits per minute.
/// </summary>
public static class CreditConfig
{
    /// <summary>Credit cost per minute. All pricing is a multiple of this.</summary>
    private const int CreditPerMinute = 2;

    /// <summary>One-time bonus for enrolling both Face and Voice biometrics (ever, per user) —
    /// see ProfileBiometricsService.EnrollAsync.</summary>
    public const int BiometricEnrollmentBonusCredits = 100;

    public static Dictionary<int, int> DurationToCreditCost { get; } = new()
    {
        { 15, CreditPerMinute * 15 },      // 15 min = 30 credits
        { 30, CreditPerMinute * 30 },      // 30 min = 60 credits
        { 45, CreditPerMinute * 45 },      // 45 min = 90 credits
        { 60, CreditPerMinute * 60 }       // 60 min = 120 credits
    };

    public static int GetCreditCost(int durationMinutes)
    {
        if (DurationToCreditCost.TryGetValue(durationMinutes, out var cost))
            return cost;
        // Fallback: linear calculation (2 credits per minute)
        return durationMinutes * CreditPerMinute;
    }

    public static bool TryGetSuitableDuration(int currentBalance, out int suggestedDuration)
    {
        suggestedDuration = 0;
        var sorted = DurationToCreditCost.OrderByDescending(x => x.Key);
        foreach (var (duration, cost) in sorted)
        {
            if (cost <= currentBalance)
            {
                suggestedDuration = duration;
                return true;
            }
        }
        return false;
    }
}
