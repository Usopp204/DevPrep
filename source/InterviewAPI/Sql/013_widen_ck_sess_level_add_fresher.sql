-- ============================================================================
-- 013_widen_ck_sess_level_add_fresher.sql — Widens CK_Sess_Level to allow
-- 'Fresher'. SessionService.ValidLevels has included "Fresher" since the
-- service was first written (SessionService.cs:60), but the SQL CHECK
-- constraint baselined from the live schema on 2026-08-12 never got the
-- matching value — a candidate configuring level "Fresher" passes C#
-- validation, then fails at SaveChangesAsync with a CHECK constraint
-- violation, surfaced to the user as a generic 503.
-- Idempotent: safe to run multiple times. Run manually in SSMS against the
-- InterviewAPI database (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_Sess_Level'
      AND parent_object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND definition NOT LIKE '%Fresher%')
BEGIN
    ALTER TABLE dbo.InterviewSessions DROP CONSTRAINT CK_Sess_Level;

    ALTER TABLE dbo.InterviewSessions WITH CHECK ADD CONSTRAINT CK_Sess_Level
        CHECK (([Level]='Lead' OR [Level]='Senior' OR [Level]='Middle' OR [Level]='Junior' OR [Level]='Intern' OR [Level]='Fresher'));

    ALTER TABLE dbo.InterviewSessions CHECK CONSTRAINT CK_Sess_Level;
END;
GO
