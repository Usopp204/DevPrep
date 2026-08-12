-- ============================================================================
-- 011_add_hint_used_at.sql — Tracks whether a hint has already been used for
-- a given SessionQuestion, and when. Enforces "one hint per question" at the
-- server (see SessionRepository.TryConsumeHintAsync). DATETIME2 (not BIT) so
-- the timestamp is also usable for reporting (hint-usage rate by difficulty).
-- Idempotent: safe to run multiple times. Run manually in SSMS against the
-- InterviewAPI database (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SessionQuestions')
      AND name = N'HintUsedAt')
BEGIN
    ALTER TABLE dbo.SessionQuestions ADD HintUsedAt DATETIME2 NULL;
END;
GO
