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

-- Bat buoc cho filtered unique index (vd UQ_Positions_Name) o phan 3 ben duoi.
SET QUOTED_IDENTIFIER ON;
GO


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

-- 1g. InterviewSessions.CameraViolationCount (mandatory-camera policy audit trail,
-- source/InterviewAPI/Sql/005_add_camera_violation_count.sql).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewSessions')
      AND name = N'CameraViolationCount')
BEGIN
    ALTER TABLE dbo.InterviewSessions ADD CameraViolationCount INT NOT NULL DEFAULT 0;
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

-- 3b. Positions catalog (PR "position-crud-admin-cleanup", 2026-07-24): Questions.Position
-- doi tu cot text tu do sang FK chuan hoa toi bang Positions moi.
IF OBJECT_ID(N'dbo.Positions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Positions
    (
        PositionId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_Positions PRIMARY KEY
            CONSTRAINT DF_Positions_PositionId DEFAULT (NEWSEQUENTIALID()),
        Name       NVARCHAR(50)     NOT NULL,
        IsDeleted  BIT              NOT NULL CONSTRAINT DF_Positions_IsDeleted DEFAULT (0),
        DeletedAt  DATETIME2        NULL,
        CreatedAt  DATETIME2        NOT NULL CONSTRAINT DF_Positions_CreatedAt DEFAULT (DATEADD(HOUR,7,SYSUTCDATETIME())),
        UpdatedAt  DATETIME2        NOT NULL CONSTRAINT DF_Positions_UpdatedAt DEFAULT (DATEADD(HOUR,7,SYSUTCDATETIME()))
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Positions_Name' AND object_id = OBJECT_ID(N'dbo.Positions'))
BEGIN
    CREATE UNIQUE INDEX UQ_Positions_Name ON dbo.Positions (Name) WHERE ([IsDeleted] = 0);
END;
GO

-- 3c. Seed Positions tu cac gia tri Position (text) da co san trong Questions — khong mat du lieu cu.
-- Dung dynamic SQL: neu cot Position (text) da bi xoa o may nao do (da qua giai doan chuyen
-- doi), T-SQL van bat loi "Invalid column name" luc PARSE batch nay cho du IF khong chay —
-- EXEC() hoan viec parse ve luc chay nen an toan ca 2 truong hop.
IF COL_LENGTH('dbo.Questions', 'Position') IS NOT NULL
BEGIN
    EXEC(N'
        INSERT INTO dbo.Positions (Name)
        SELECT DISTINCT q.Position
        FROM dbo.Questions q
        WHERE q.Position IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM dbo.Positions p WHERE p.Name = q.Position);
    ');
END;
GO

-- 3d. Questions.PositionId (cot FK moi).
IF COL_LENGTH('dbo.Questions', 'PositionId') IS NULL
BEGIN
    ALTER TABLE dbo.Questions ADD PositionId UNIQUEIDENTIFIER NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Q_Position')
BEGIN
    ALTER TABLE dbo.Questions ADD CONSTRAINT FK_Q_Position
        FOREIGN KEY (PositionId) REFERENCES dbo.Positions(PositionId);
END;
GO

-- 3e. Backfill PositionId tu cot text cu (chi cac dong chua duoc gan). Dynamic SQL — ly do
-- giong 3c o tren.
IF COL_LENGTH('dbo.Questions', 'Position') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE q
        SET q.PositionId = p.PositionId
        FROM dbo.Questions q
        JOIN dbo.Positions p ON p.Name = q.Position
        WHERE q.PositionId IS NULL AND q.Position IS NOT NULL;
    ');
END;
GO

-- LUU Y: cot Questions.Position (text) cu KHONG bi xoa o day de an toan — code C# da
-- khong con doc/ghi cot nay nua (chi con PositionId), giu lai lam backup doi chieu.
-- Neu sau khi kiem tra ky (PositionId da gan dung het) thi co the DROP COLUMN Position
-- thu cong sau.


-- ============================================================================
-- 4) DevPrep_Payment
-- ============================================================================
USE [DevPrep_Payment];
GO

-- 4a. Chan trung thuong bonus dang ky sinh trac hoc (source/PaymentAPI/Sql/001_add_credit_ledger_bonus_index.sql).
-- Toi da 1 dong "Bonus" moi user — check-then-insert phia code khong du an toan khi co
-- 2 request cung luc, unique filtered index nay la hang rao that su o tang DB.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Ledger_UserBonus'
      AND object_id = OBJECT_ID(N'dbo.CreditLedger'))
BEGIN
    CREATE UNIQUE INDEX UX_Ledger_UserBonus
        ON dbo.CreditLedger (UserId)
        WHERE EntryType = 'Bonus';
END;
GO


-- ============================================================================
-- Xong. Khong co gi de sua o 7 database con lai (Auth, CvJd, User,
-- News, Feedback, Statistics, Notification) tai thoi diem viet script nay —
-- da doi chieu tung cot voi model C# tuong ung, khop 100%.
-- ============================================================================
PRINT 'Dong bo schema hoan tat.';
GO
