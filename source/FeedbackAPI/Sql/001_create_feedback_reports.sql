-- Idempotent script to create FeedbackReports table.
-- Safe to run multiple times; uses IF NOT EXISTS checks.
-- This is schema-only: it intentionally does not add sample reports that could
-- alter the visibility or moderation state of existing System/AI feedback.
USE [DevPrep_Feedback];
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
