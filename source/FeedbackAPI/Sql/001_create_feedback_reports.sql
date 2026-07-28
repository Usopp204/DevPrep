-- Idempotent schema upgrade for feedback moderation and reporting.
-- Safe to run multiple times. Existing, non-deleted feedback is approved and
-- kept visible when the moderation columns are introduced so legacy demo data
-- does not disappear. New feedback remains Pending and hidden by default.
USE [DevPrep_Feedback];
GO

SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.SystemFeedbacks', N'U') IS NULL
    THROW 50001, 'dbo.SystemFeedbacks does not exist. Run the base Feedback database script first.', 1;

IF OBJECT_ID(N'dbo.InterviewFeedbacks', N'U') IS NULL
    THROW 50002, 'dbo.InterviewFeedbacks does not exist. Run the base Feedback database script first.', 1;
GO

-- System feedback ratings.
IF COL_LENGTH(N'dbo.SystemFeedbacks', N'UiUxRating') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD UiUxRating TINYINT NULL;

IF COL_LENGTH(N'dbo.SystemFeedbacks', N'UsabilityRating') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD UsabilityRating TINYINT NULL;

IF COL_LENGTH(N'dbo.SystemFeedbacks', N'FeatureQualityRating') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD FeatureQualityRating TINYINT NULL;
GO

-- System feedback moderation. Backfill legacy rows before applying the defaults
-- used for newly submitted feedback.
IF COL_LENGTH(N'dbo.SystemFeedbacks', N'ModerationStatus') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD ModerationStatus NVARCHAR(20) NULL;

EXEC sys.sp_executesql N'
    UPDATE dbo.SystemFeedbacks
    SET ModerationStatus = CASE WHEN IsDeleted = 0 THEN N''Approved'' ELSE N''Pending'' END
    WHERE ModerationStatus IS NULL;';

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SystemFeedbacks')
      AND name = N'ModerationStatus'
      AND is_nullable = 1
)
    ALTER TABLE dbo.SystemFeedbacks ALTER COLUMN ModerationStatus NVARCHAR(20) NOT NULL;
GO

IF COL_LENGTH(N'dbo.SystemFeedbacks', N'IsVisible') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD IsVisible BIT NULL;

EXEC sys.sp_executesql N'
    UPDATE dbo.SystemFeedbacks
    SET IsVisible = CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END
    WHERE IsVisible IS NULL;';

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.SystemFeedbacks')
      AND name = N'IsVisible'
      AND is_nullable = 1
)
    ALTER TABLE dbo.SystemFeedbacks ALTER COLUMN IsVisible BIT NOT NULL;
GO

IF COL_LENGTH(N'dbo.SystemFeedbacks', N'ModeratedAt') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD ModeratedAt DATETIME2 NULL;

IF COL_LENGTH(N'dbo.SystemFeedbacks', N'ModeratedBy') IS NULL
    ALTER TABLE dbo.SystemFeedbacks ADD ModeratedBy UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SystemFeedbacks')
      AND c.name = N'ModerationStatus'
)
    ALTER TABLE dbo.SystemFeedbacks
        ADD CONSTRAINT DF_SystemFeedbacks_ModerationStatus
        DEFAULT (N'Pending') FOR ModerationStatus;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SystemFeedbacks')
      AND c.name = N'IsVisible'
)
    ALTER TABLE dbo.SystemFeedbacks
        ADD CONSTRAINT DF_SystemFeedbacks_IsVisible
        DEFAULT ((0)) FOR IsVisible;
GO

-- Interview/AI feedback ratings.
IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'QuestionQualityRating') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD QuestionQualityRating TINYINT NULL;

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'AiAccuracyRating') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD AiAccuracyRating TINYINT NULL;

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'ScoringFairnessRating') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD ScoringFairnessRating TINYINT NULL;

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'FeedbackUsefulnessRating') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD FeedbackUsefulnessRating TINYINT NULL;
GO

-- Interview/AI feedback moderation.
IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'ModerationStatus') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD ModerationStatus NVARCHAR(20) NULL;

EXEC sys.sp_executesql N'
    UPDATE dbo.InterviewFeedbacks
    SET ModerationStatus = CASE WHEN IsDeleted = 0 THEN N''Approved'' ELSE N''Pending'' END
    WHERE ModerationStatus IS NULL;';

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewFeedbacks')
      AND name = N'ModerationStatus'
      AND is_nullable = 1
)
    ALTER TABLE dbo.InterviewFeedbacks ALTER COLUMN ModerationStatus NVARCHAR(20) NOT NULL;
GO

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'IsVisible') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD IsVisible BIT NULL;

EXEC sys.sp_executesql N'
    UPDATE dbo.InterviewFeedbacks
    SET IsVisible = CASE WHEN IsDeleted = 0 THEN 1 ELSE 0 END
    WHERE IsVisible IS NULL;';

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.InterviewFeedbacks')
      AND name = N'IsVisible'
      AND is_nullable = 1
)
    ALTER TABLE dbo.InterviewFeedbacks ALTER COLUMN IsVisible BIT NOT NULL;
GO

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'ModeratedAt') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD ModeratedAt DATETIME2 NULL;

IF COL_LENGTH(N'dbo.InterviewFeedbacks', N'ModeratedBy') IS NULL
    ALTER TABLE dbo.InterviewFeedbacks ADD ModeratedBy UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.InterviewFeedbacks')
      AND c.name = N'ModerationStatus'
)
    ALTER TABLE dbo.InterviewFeedbacks
        ADD CONSTRAINT DF_InterviewFeedbacks_ModerationStatus
        DEFAULT (N'Pending') FOR ModerationStatus;

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.InterviewFeedbacks')
      AND c.name = N'IsVisible'
)
    ALTER TABLE dbo.InterviewFeedbacks
        ADD CONSTRAINT DF_InterviewFeedbacks_IsVisible
        DEFAULT ((0)) FOR IsVisible;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'FeedbackReports' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE [dbo].[FeedbackReports] (
        [FeedbackReportId] UNIQUEIDENTIFIER NOT NULL DEFAULT (newsequentialid()),
        [FeedbackType] NVARCHAR(20) NOT NULL,
        [InterviewFeedbackId] UNIQUEIDENTIFIER NULL,
        [SystemFeedbackId] UNIQUEIDENTIFIER NULL,
        [ReporterUserId] UNIQUEIDENTIFIER NOT NULL,
        [ReasonCode] NVARCHAR(50) NOT NULL,
        [Details] NVARCHAR(1000) NULL,
        [Status] NVARCHAR(40) NOT NULL DEFAULT ('Pending'),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (dateadd(hour,(7),sysutcdatetime())),
        [ReviewedAt] DATETIME2 NULL,
        [ReviewedBy] UNIQUEIDENTIFIER NULL,
        [ResolutionNote] NVARCHAR(1000) NULL,

        CONSTRAINT [PK_FeedbackReports] PRIMARY KEY ([FeedbackReportId]),

        CONSTRAINT [CK_FeedbackReports_FeedbackType]
            CHECK ([FeedbackType] IN (N'System', N'Interview')),
        CONSTRAINT [CK_FeedbackReports_ExactlyOneTarget]
            CHECK (([InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL)
                OR ([InterviewFeedbackId] IS NULL AND [SystemFeedbackId] IS NOT NULL)),
        CONSTRAINT [CK_FeedbackReports_TypeMatchesTarget]
            CHECK (([FeedbackType] = N'Interview' AND [InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL)
                OR ([FeedbackType] = N'System' AND [SystemFeedbackId] IS NOT NULL AND [InterviewFeedbackId] IS NULL)),
        CONSTRAINT [CK_FeedbackReports_ReasonCode]
            CHECK ([ReasonCode] IN (N'HarassmentOrPersonalAttack', N'HateOrDiscrimination', N'ThreatsOrViolence',
                                    N'SexualOrInappropriateContent', N'SpamOrMisleading',
                                    N'PrivacyOrPersonalInformation', N'OffTopicOrIrrelevant', N'Other')),
        CONSTRAINT [CK_FeedbackReports_Status]
            CHECK ([Status] IN (N'Pending', N'Dismissed', N'ResolvedByFeedbackDeletion')),

        CONSTRAINT [FK_FeedbackReports_InterviewFeedbacks]
            FOREIGN KEY ([InterviewFeedbackId])
            REFERENCES [dbo].[InterviewFeedbacks] ([InterviewFeedbackId])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_FeedbackReports_SystemFeedbacks]
            FOREIGN KEY ([SystemFeedbackId])
            REFERENCES [dbo].[SystemFeedbacks] ([FeedbackId])
            ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [UX_FeedbackReports_Reporter_Interview]
        ON [FeedbackReports] ([ReporterUserId], [InterviewFeedbackId])
        WHERE [InterviewFeedbackId] IS NOT NULL;

    CREATE UNIQUE INDEX [UX_FeedbackReports_Reporter_System]
        ON [FeedbackReports] ([ReporterUserId], [SystemFeedbackId])
        WHERE [SystemFeedbackId] IS NOT NULL;

    CREATE INDEX [IX_FeedbackReports_Status_CreatedAt]
        ON [FeedbackReports] ([Status], [CreatedAt]);

    CREATE INDEX [IX_FeedbackReports_InterviewFeedbackId]
        ON [FeedbackReports] ([InterviewFeedbackId]);

    CREATE INDEX [IX_FeedbackReports_SystemFeedbackId]
        ON [FeedbackReports] ([SystemFeedbackId]);

    CREATE INDEX [IX_FeedbackReports_ReporterUserId]
        ON [FeedbackReports] ([ReporterUserId]);

    CREATE INDEX [IX_FeedbackReports_ReasonCode]
        ON [FeedbackReports] ([ReasonCode]);
END
GO
