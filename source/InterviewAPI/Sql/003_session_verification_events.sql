-- ============================================================================
-- 003_session_verification_events.sql — Audit log for entry-gate and randomized
-- in-session biometric re-checks (Milestone 4 groundwork). Idempotent: safe to
-- run multiple times. Run manually in SSMS against the InterviewAPI database
-- (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Interview];
GO

IF OBJECT_ID(N'dbo.SessionVerificationEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionVerificationEvents
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SessionVerificationEvents PRIMARY KEY,
        SessionId       UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        Phase           NVARCHAR(16)     NOT NULL,   -- 'Entry' | 'InSession'
        Modality        NVARCHAR(16)     NOT NULL,   -- 'Face' | 'Voice'
        Score           FLOAT            NULL,
        LivenessPassed  BIT              NULL,        -- face only; NULL for voice
        Matched         BIT              NOT NULL,
        Decision        NVARCHAR(16)     NOT NULL,   -- 'Pass' | 'Fail' | 'Flag'
        Detail          NVARCHAR(MAX)    NULL,        -- JSON: reason, transcript, etc.
        CreatedAt       DATETIME2        NOT NULL
            CONSTRAINT DF_SessionVerificationEvents_CreatedAt DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME()))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SVE_Session_Phase'
      AND object_id = OBJECT_ID(N'dbo.SessionVerificationEvents'))
BEGIN
    CREATE INDEX IX_SVE_Session_Phase
        ON dbo.SessionVerificationEvents (SessionId, Phase);
END;
GO
