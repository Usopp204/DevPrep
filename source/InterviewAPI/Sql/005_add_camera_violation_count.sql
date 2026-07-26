-- ============================================================================
-- 005_add_camera_violation_count.sql — Tracks camera-consent/connectivity flags
-- for the Room's mandatory-camera policy (permission denied, or disconnected
-- mid-session). Flag-only (per Biometrics:InSessionStrictness="Flag"): the
-- interview is never blocked or auto-ended by this counter, it is just an
-- audit signal for later review. Idempotent: safe to run multiple times. Run
-- manually in SSMS against the InterviewAPI database (adjust the USE line if
-- your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND name = N'CameraViolationCount')
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD CameraViolationCount INT NOT NULL DEFAULT 0;
END;
GO
