-- ============================================================================
-- 010_add_integrity_policy_acceptance.sql — Tracks whether the user has
-- read and accepted the Interview Integrity Policy before entering the Room.
-- Idempotent: safe to run multiple times. Run manually in SSMS against the
-- InterviewAPI database (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND name = N'IntegrityPolicyAccepted')
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD IntegrityPolicyAccepted BIT NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND name = N'IntegrityPolicyAcceptedAt')
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD IntegrityPolicyAcceptedAt DATETIME2 NULL;
END;
GO
