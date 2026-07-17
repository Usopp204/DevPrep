-- ============================================================================
-- 002_session_verification_challenges.sql — Server-side verification challenges
-- (Milestone 2). Idempotent: safe to run multiple times. Run manually in SSMS
-- against the InterviewAPI database (adjust the USE line if your DB name differs).
-- The expected challenge value is stored server-side only and never chosen by
-- the client — this is what defeats static-photo and replay attacks.
-- ============================================================================
USE [DevPrep_Interview];
GO

IF OBJECT_ID(N'dbo.SessionVerificationChallenges', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionVerificationChallenges
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SessionVerificationChallenges PRIMARY KEY,
        SessionId     UNIQUEIDENTIFIER NOT NULL,
        UserId        UNIQUEIDENTIFIER NOT NULL,
        Modality      NVARCHAR(16)     NOT NULL,   -- 'Face' | 'Voice'
        ChallengeJson NVARCHAR(400)    NOT NULL,   -- {"type":"turn_head","direction":"right"} / {"phrase":"..."}
        ExpiresAt     DATETIME2        NOT NULL,   -- short TTL (Biometrics:ChallengeTtlSeconds)
        ConsumedAt    DATETIME2        NULL,       -- set on first verify attempt (anti-replay)
        CreatedAt     DATETIME2        NOT NULL
            CONSTRAINT DF_SessionVerificationChallenges_CreatedAt DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME()))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SVC_Session_Modality'
      AND object_id = OBJECT_ID(N'dbo.SessionVerificationChallenges'))
BEGIN
    CREATE INDEX IX_SVC_Session_Modality
        ON dbo.SessionVerificationChallenges (SessionId, Modality);
END;
GO
