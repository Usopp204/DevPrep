-- ============================================================================
-- 012_add_last_skip_at.sql — Tracks the last time the user used "Skip" in a
-- session, to enforce a per-session cooldown (Interview:SkipCooldownSeconds,
-- default 10s) and stop rapid-fire Skip spam. Enforced server-side via
-- SessionRepository.TryConsumeSkipCooldownAsync (atomic conditional UPDATE).
-- Idempotent: safe to run multiple times. Run manually in SSMS against the
-- InterviewAPI database (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND name = N'LastSkipAtUtc')
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD LastSkipAtUtc DATETIME2 NULL;
END;
GO
