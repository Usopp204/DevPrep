namespace WebClient.Helpers;

// Mirrors InterviewAPI's CreditConfig.cs (2 credits/minute) so the Dashboard can show a price
// next to each duration option and pre-check affordability before Start. WebClient has no direct
// access to that class (different process/assembly) and there is no pricing endpoint yet, so
// this is intentionally duplicated — if CreditConfig.cs's pricing ever changes, update this to
// match by hand.
public static class CreditPricing
{
    public static readonly IReadOnlyDictionary<int, int> DurationToCreditCost = new Dictionary<int, int>
    {
        { 15, 30 },
        { 30, 60 },
        { 45, 90 },
        { 60, 120 }
    };
}
