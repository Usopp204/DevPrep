namespace InterviewAPI.Helpers;

/// <summary>
/// Centralized credit pricing configuration. Change here affects all credit-related logic
/// (deduction, validation, UI messages). Temporary pricing: 1 credit per 15 minutes.
/// </summary>
public static class CreditConfig
{
    /// <summary>Credit cost per 15 minutes. All pricing is a multiple of this.</summary>
    private const int CreditPer15Minutes = 1;

    public static Dictionary<int, int> DurationToCreditCost { get; } = new()
    {
        { 15, CreditPer15Minutes * 1 },      // 15 min = 1 credit
        { 30, CreditPer15Minutes * 2 },      // 30 min = 2 credits
        { 45, CreditPer15Minutes * 3 },      // 45 min = 3 credits
        { 60, CreditPer15Minutes * 4 }       // 60 min = 4 credits
    };

    public static int GetCreditCost(int durationMinutes)
    {
        if (DurationToCreditCost.TryGetValue(durationMinutes, out var cost))
            return cost;
        // Fallback: linear calculation (1 credit per 15 min)
        return (durationMinutes + 14) / 15;
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
