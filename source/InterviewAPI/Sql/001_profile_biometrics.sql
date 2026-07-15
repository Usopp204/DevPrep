-- ============================================================================
-- 001_profile_biometrics.sql — Biometric enrollment storage (Milestone 1).
-- Idempotent: safe to run multiple times. Run manually in SSMS against the
-- InterviewAPI database (adjust the USE line if your database name differs).
-- Stores embeddings only (no raw media) per privacy requirements.
-- ============================================================================
USE [DevPrep_Interview];
GO

IF OBJECT_ID(N'dbo.ProfileBiometrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfileBiometrics
    (
        Id           BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ProfileBiometrics PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER     NOT NULL,
        Modality     NVARCHAR(16)         NOT NULL,   -- 'Face' | 'Voice'
        Embedding    VARBINARY(MAX)       NOT NULL,   -- float32[] little-endian (EmbeddingCodec)
        Dim          INT                  NOT NULL,   -- 512 (face) / 192 (voice)
        ModelVersion NVARCHAR(128)        NOT NULL,
        Quality      FLOAT                NULL,       -- face only; NULL for voice
        CreatedAt    DATETIME2            NOT NULL
            CONSTRAINT DF_ProfileBiometrics_CreatedAt DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME())),
        UpdatedAt    DATETIME2            NOT NULL
            CONSTRAINT DF_ProfileBiometrics_UpdatedAt DEFAULT (DATEADD(HOUR, 7, SYSUTCDATETIME()))
    );
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_ProfileBiometrics_User_Modality'
      AND object_id = OBJECT_ID(N'dbo.ProfileBiometrics'))
BEGIN
    CREATE UNIQUE INDEX UX_ProfileBiometrics_User_Modality
        ON dbo.ProfileBiometrics (UserId, Modality);
END;
GO
