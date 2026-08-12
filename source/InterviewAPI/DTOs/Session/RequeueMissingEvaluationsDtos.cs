namespace InterviewAPI.DTOs.Session;

/// <summary>
/// Asks InterviewAPI to re-queue grading work for answers whose original grading job was lost
/// (see SessionService.RequeueMissingEvaluationsAsync). EvaluationAPI sends the SessionQuestionIds
/// it found missing while building a report — it cannot build the work itself because the grading
/// payload needs SessionQuestion.ReferenceAnswer, which never leaves InterviewAPI.
/// </summary>
public class RequeueMissingEvaluationsRequest
{
    public List<Guid> SessionQuestionIds { get; set; } = new();
}

public class RequeueMissingEvaluationsResponse
{
    public Guid SessionId { get; set; }

    /// <summary>How many ids the caller asked about.</summary>
    public int RequestedCount { get; set; }

    /// <summary>Accepted onto the grading queue by this call.</summary>
    public int EnqueuedCount { get; set; }

    /// <summary>Eligible, but the queue was full or shutting down — nothing was queued for these.</summary>
    public int DroppedCount { get; set; }

    /// <summary>
    /// Ids that do not correspond to a question with a substantive answer in this session (already
    /// skipped, timed out, unknown id). Nothing to grade, so nothing was queued — not an error.
    /// </summary>
    public int NotEligibleCount { get; set; }

    /// <summary>True when the per-session cooldown suppressed this burst entirely.</summary>
    public bool CooldownActive { get; set; }

    public int CooldownSecondsRemaining { get; set; }
}
