IF COL_LENGTH('dbo.SessionVerificationEvents', 'ChallengeJson') IS NULL
BEGIN
    ALTER TABLE dbo.SessionVerificationEvents
    ADD ChallengeJson NVARCHAR(400) NULL;
END;
GO
