-- ============================================================================
-- 006_add_camera_incident_id.sql — Adds an idempotency key for camera incidents
-- reported by the Interview Room. Run manually in SSMS against InterviewAPI.
-- ============================================================================
USE [DevPrep_Interview];
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SessionVerificationEvents')
      AND name = N'IncidentId')
BEGIN
    ALTER TABLE dbo.SessionVerificationEvents
        ADD IncidentId UNIQUEIDENTIFIER NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SessionVerificationEvents')
      AND name = N'UX_SVE_IncidentId')
BEGIN
    CREATE UNIQUE INDEX UX_SVE_IncidentId
        ON dbo.SessionVerificationEvents (IncidentId)
        WHERE IncidentId IS NOT NULL;
END;
GO
