ALTER TABLE dbo.SessionVerificationEvents
ADD SessionQuestionId UNIQUEIDENTIFIER NULL,
    ExpiresAt DATETIME2 NULL,
    ConsumedAt DATETIME2 NULL,
    ChallengeJson NVARCHAR(400) NULL;
GO

CREATE UNIQUE INDEX UX_SVE_Session_Question_Check
ON dbo.SessionVerificationEvents(SessionId, SessionQuestionId)
WHERE SessionQuestionId IS NOT NULL;
GO
