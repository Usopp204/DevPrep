-- ============================================================================
-- sync_all_databases.sql — Dong bo TAT CA database local cho khop voi code C#
-- hien tai (dev khong dung EF Migrations, DB phai tay maintain thu cong).
--
-- AN TOAN CHAY NHIEU LAN (idempotent): moi thay doi deu duoc bao boc trong
-- IF kiem tra truoc, da co roi thi bo qua, KHONG bao gio xoa du lieu.
--
-- Cach chay:
--   sqlcmd -S <TEN_SERVER_SQL_CUA_BAN> -U sa -P <password> -C -i sync_all_databases.sql
--   (hoac mo file nay bang SSMS / Azure Data Studio roi bam Execute)
--
-- Yeu cau: 11 database DevPrep_* da duoc tao san (Payment, Auth, Evaluation,
-- Interview, CvJd, Question, Statistics, User, News, Feedback, Notification).
-- Script nay CHI vá schema (them cot/bang/sua constraint con thieu), khong
-- tao database tu dau.
-- ============================================================================


-- ============================================================================
-- 1) DevPrep_Interview
-- ============================================================================
USE [DevPrep_Interview];
GO

-- 1a. InterviewSessions.Position — them boi tinh nang loc theo vi tri ung tuyen.
IF COL_LENGTH('dbo.InterviewSessions', 'Position') IS NULL
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD Position NVARCHAR(100) NULL;
END;
GO

-- 1b. SessionQuestions.ReferenceAnswer — them boi tinh nang cham diem AI can dap an mau.
IF COL_LENGTH('dbo.SessionQuestions', 'ReferenceAnswer') IS NULL
BEGIN
    ALTER TABLE dbo.SessionQuestions ADD ReferenceAnswer NVARCHAR(MAX) NULL;
END;
GO

-- 1c. CK_Sess_Status phai cho phep 'Paused' (tinh nang Pause/Resume Room UX Phase 2).
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_Sess_Status'
      AND parent_object_id = OBJECT_ID('dbo.InterviewSessions')
      AND definition NOT LIKE '%Paused%')
BEGIN
    ALTER TABLE dbo.InterviewSessions DROP CONSTRAINT CK_Sess_Status;
    ALTER TABLE dbo.InterviewSessions ADD CONSTRAINT CK_Sess_Status
        CHECK ([Status]='Cancelled' OR [Status]='Expired' OR [Status]='Completed'
            OR [Status]='InProgress' OR [Status]='Initialized' OR [Status]='Configured'
            OR [Status]='Paused');
END;
GO

-- 1d. ProfileBiometrics (Milestone 1 - biometric anti-cheating).
IF OBJECT_ID(N'dbo.ProfileBiometrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProfileBiometrics
    (
        Id           BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_ProfileBiometrics PRIMARY KEY,
        UserId       UNIQUEIDENTIFIER     NOT NULL,
        Modality     NVARCHAR(16)         NOT NULL,
        Embedding    VARBINARY(MAX)       NOT NULL,
        Dim          INT                  NOT NULL,
        ModelVersion NVARCHAR(128)        NOT NULL,
        Quality      FLOAT                NULL,
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

-- 1e. SessionVerificationChallenges (Milestone 2 - biometric anti-cheating).
IF OBJECT_ID(N'dbo.SessionVerificationChallenges', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionVerificationChallenges
    (
        Id            BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SessionVerificationChallenges PRIMARY KEY,
        SessionId     UNIQUEIDENTIFIER NOT NULL,
        UserId        UNIQUEIDENTIFIER NOT NULL,
        Modality      NVARCHAR(16)     NOT NULL,
        ChallengeJson NVARCHAR(400)    NOT NULL,
        ExpiresAt     DATETIME2        NOT NULL,
        ConsumedAt    DATETIME2        NULL,
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

-- 1f. SessionVerificationEvents (Milestone 4 - biometric anti-cheating).
IF OBJECT_ID(N'dbo.SessionVerificationEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionVerificationEvents
    (
        Id              BIGINT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_SessionVerificationEvents PRIMARY KEY,
        SessionId       UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        Phase           NVARCHAR(16)     NOT NULL,
        Modality        NVARCHAR(16)     NOT NULL,
        Score           FLOAT            NULL,
        LivenessPassed  BIT              NULL,
        Matched         BIT              NOT NULL,
        Decision        NVARCHAR(16)     NOT NULL,
        Detail          NVARCHAR(MAX)    NULL,
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


-- ============================================================================
-- 2) DevPrep_Evaluation
-- ============================================================================
USE [DevPrep_Evaluation];
GO

-- 2a. AnswerEvaluations: cot cu ten "EvaluatedAt", model C# dung "GeneratedAt".
IF COL_LENGTH('dbo.AnswerEvaluations', 'EvaluatedAt') IS NOT NULL
   AND COL_LENGTH('dbo.AnswerEvaluations', 'GeneratedAt') IS NULL
BEGIN
    EXEC sp_rename 'dbo.AnswerEvaluations.EvaluatedAt', 'GeneratedAt', 'COLUMN';
END;
GO


-- ============================================================================
-- 3) DevPrep_Question
-- ============================================================================
USE [DevPrep_Question];
GO

-- 3a. Questions.Difficulty: DEFAULT DB dang la 'Medium' nhung CK_Questions_Difficulty
-- chi cho phep Senior/Middle/Junior/Fresher/Intern ('Medium' KHONG hop le) va code C#
-- luon set "Junior" khi tao moi. Sua default cho dong bo, tranh loi neu sau nay co
-- insert khong set Difficulty tuong minh.
IF EXISTS (
    SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Questions')
      AND c.name = 'Difficulty'
      AND dc.definition NOT LIKE '%Junior%')
BEGIN
    DECLARE @dfName NVARCHAR(128);
    SELECT @dfName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Questions') AND c.name = 'Difficulty';

    IF @dfName IS NOT NULL
        EXEC('ALTER TABLE dbo.Questions DROP CONSTRAINT ' + @dfName);

    ALTER TABLE dbo.Questions ADD CONSTRAINT DF_Questions_Difficulty DEFAULT ('Junior') FOR Difficulty;
END;
GO


-- ============================================================================
-- Xong. Khong co gi de sua o 8 database con lai (Payment, Auth, CvJd, User,
-- News, Feedback, Statistics, Notification) tai thoi diem viet script nay —
-- da doi chieu tung cot voi model C# tuong ung, khop 100%.
-- ============================================================================
PRINT 'Dong bo schema hoan tat.';
GO
