namespace FeedbackAPI.Validation;

public static class FeedbackReportType
{
    public const string System = "System";
    public const string Interview = "Interview";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        System,
        Interview
    };
}

public static class FeedbackReportReason
{
    public const string HarassmentOrPersonalAttack = "HarassmentOrPersonalAttack";
    public const string HateOrDiscrimination = "HateOrDiscrimination";
    public const string ThreatsOrViolence = "ThreatsOrViolence";
    public const string SexualOrInappropriateContent = "SexualOrInappropriateContent";
    public const string SpamOrMisleading = "SpamOrMisleading";
    public const string PrivacyOrPersonalInformation = "PrivacyOrPersonalInformation";
    public const string OffTopicOrIrrelevant = "OffTopicOrIrrelevant";
    public const string Other = "Other";

    public static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HarassmentOrPersonalAttack] = "Harassment or personal attack",
            [HateOrDiscrimination] = "Hate or discrimination",
            [ThreatsOrViolence] = "Threats or violent content",
            [SexualOrInappropriateContent] = "Sexual or inappropriate content",
            [SpamOrMisleading] = "Spam, scam, or misleading information",
            [PrivacyOrPersonalInformation] = "Privacy or personal information",
            [OffTopicOrIrrelevant] = "Off-topic or irrelevant content",
            [Other] = "Other"
        };
}

public static class FeedbackReportStatus
{
    public const string Pending = "Pending";
    public const string Dismissed = "Dismissed";
    public const string ResolvedByFeedbackDeletion = "ResolvedByFeedbackDeletion";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Dismissed,
        ResolvedByFeedbackDeletion
    };
}

public static class FeedbackReportInput
{
    public const int MaxDetailsLength = 1000;
    public const int MaxResolutionNoteLength = 1000;

    public static string? NormalizeOptional(string? value, int maximumLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maximumLength)
        {
            throw new FeedbackAPI.Services.FeedbackReportValidationException(
                $"{fieldName} cannot exceed {maximumLength} characters.");
        }

        return value;
    }
}
