IF COL_LENGTH('dbo.SessionVerificationEvents', 'CheckSequence') IS NULL
BEGIN
    ALTER TABLE dbo.SessionVerificationEvents
    ADD CheckSequence INT NULL;
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SVE_Session_Question_Check'
      AND object_id = OBJECT_ID(N'dbo.SessionVerificationEvents'))
BEGIN
    DROP INDEX UX_SVE_Session_Question_Check
    ON dbo.SessionVerificationEvents;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SVE_Session_Check_Sequence'
      AND object_id = OBJECT_ID(N'dbo.SessionVerificationEvents'))
BEGIN
    CREATE UNIQUE INDEX UX_SVE_Session_Check_Sequence
    ON dbo.SessionVerificationEvents(SessionId, CheckSequence)
    WHERE CheckSequence IS NOT NULL;
END;
GO
