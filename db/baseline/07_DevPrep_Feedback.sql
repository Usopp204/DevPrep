-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Feedback   (owned by FeedbackAPI, source/FeedbackAPI/Models/FeedbackDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Feedback -> Tasks -> Generate Scripts
--        -> Select specific database objects: Tables, Views, Stored Procedures,
--           User-Defined Functions, User-Defined Types.
--           Do NOT select Users, Roles, Schemas, or Database Triggers.
--        -> Advanced: see db/README.md section 2 for the exact option values.
--           Configure them ONCE and the output needs no hand-editing.
--
-- HARD RULES FOR THIS FILE
--   1. SCHEMA ONLY. No user rows, no personal data. This file is committed to a
--      shared repository. (Seed/lookup rows are allowed only in the four files
--      that say so: Question, Payment, Notification — see db/README.md.)
--   2. NO "USE [DevPrep_Feedback]" statement. init.sh selects the database with
--      sqlcmd -d, so this file stays portable and the database name lives in
--      exactly one place.
--   3. NO "CREATE DATABASE". init.sh creates all ten catalogs before applying
--      any baseline. SSMS emits absolute Windows FILENAME paths that cannot
--      work inside a Linux container.
--   4. Keep the GO batch separators. sqlcmd needs them.
--
-- STRIP THESE IF THEY APPEAR ANYWAY (each has an SSMS option that prevents it;
-- if you see one, that option was not set — fix the export, do not hand-edit):
--   USE [...]                          ALTER AUTHORIZATION ON DATABASE::...
--   CREATE DATABASE / ON PRIMARY (...) CREATE USER / CREATE ROLE / GRANT
--   COLLATE SQL_Latin1_General_...     EXEC sys.sp_addextendedproperty
--   ALTER DATABASE ... SET ...         any C:\ or absolute filesystem path
--
-- VERIFY BEFORE COMMITTING (must return nothing):
--   grep -vE "^\s*--" db/baseline/07_DevPrep_Feedback.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[FeedbackReplies]    Script Date: 11/08/2026 6:15:26 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FeedbackReplies](
	[ReplyId] [uniqueidentifier] NOT NULL,
	[FeedbackId] [uniqueidentifier] NOT NULL,
	[RepliedBy] [uniqueidentifier] NOT NULL,
	[ReplyContent] [nvarchar](max) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_FeedbackReplies] PRIMARY KEY CLUSTERED 
(
	[ReplyId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FeedbackReports]    Script Date: 11/08/2026 6:15:26 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FeedbackReports](
	[FeedbackReportId] [uniqueidentifier] NOT NULL,
	[FeedbackType] [nvarchar](20) NOT NULL,
	[InterviewFeedbackId] [uniqueidentifier] NULL,
	[SystemFeedbackId] [uniqueidentifier] NULL,
	[ReporterUserId] [uniqueidentifier] NOT NULL,
	[ReasonCode] [nvarchar](50) NOT NULL,
	[Details] [nvarchar](1000) NULL,
	[Status] [nvarchar](40) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ReviewedAt] [datetime2](7) NULL,
	[ReviewedBy] [uniqueidentifier] NULL,
	[ResolutionNote] [nvarchar](1000) NULL,
 CONSTRAINT [PK_FeedbackReports] PRIMARY KEY CLUSTERED 
(
	[FeedbackReportId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InterviewFeedbacks]    Script Date: 11/08/2026 6:15:26 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InterviewFeedbacks](
	[InterviewFeedbackId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Rating] [tinyint] NOT NULL,
	[Content] [nvarchar](max) NULL,
	[IsHelpful] [bit] NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeletedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[ModerationStatus] [nvarchar](20) NOT NULL,
	[IsVisible] [bit] NOT NULL,
	[ModeratedAt] [datetime2](7) NULL,
	[ModeratedBy] [uniqueidentifier] NULL,
	[QuestionQualityRating] [tinyint] NULL,
	[AiAccuracyRating] [tinyint] NULL,
	[ScoringFairnessRating] [tinyint] NULL,
	[FeedbackUsefulnessRating] [tinyint] NULL,
 CONSTRAINT [PK_InterviewFeedbacks] PRIMARY KEY CLUSTERED 
(
	[InterviewFeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SystemFeedbacks]    Script Date: 11/08/2026 6:15:26 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SystemFeedbacks](
	[FeedbackId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Category] [nvarchar](30) NOT NULL,
	[Subject] [nvarchar](255) NULL,
	[Content] [nvarchar](max) NOT NULL,
	[Rating] [tinyint] NULL,
	[Status] [nvarchar](20) NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeletedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[ModerationStatus] [nvarchar](20) NOT NULL,
	[IsVisible] [bit] NOT NULL,
	[ModeratedAt] [datetime2](7) NULL,
	[ModeratedBy] [uniqueidentifier] NULL,
	[UiUxRating] [tinyint] NULL,
	[UsabilityRating] [tinyint] NULL,
	[FeatureQualityRating] [tinyint] NULL,
 CONSTRAINT [PK_SystemFeedbacks] PRIMARY KEY CLUSTERED 
(
	[FeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Index [IX_Reply_Feedback]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_Reply_Feedback] ON [dbo].[FeedbackReplies]
(
	[FeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FeedbackReports_InterviewFeedbackId]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackReports_InterviewFeedbackId] ON [dbo].[FeedbackReports]
(
	[InterviewFeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FeedbackReports_ReasonCode]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackReports_ReasonCode] ON [dbo].[FeedbackReports]
(
	[ReasonCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FeedbackReports_ReporterUserId]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackReports_ReporterUserId] ON [dbo].[FeedbackReports]
(
	[ReporterUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FeedbackReports_Status_CreatedAt]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackReports_Status_CreatedAt] ON [dbo].[FeedbackReports]
(
	[Status] ASC,
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FeedbackReports_SystemFeedbackId]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_FeedbackReports_SystemFeedbackId] ON [dbo].[FeedbackReports]
(
	[SystemFeedbackId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_FeedbackReports_Reporter_Interview]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_FeedbackReports_Reporter_Interview] ON [dbo].[FeedbackReports]
(
	[ReporterUserId] ASC,
	[InterviewFeedbackId] ASC
)
WHERE ([InterviewFeedbackId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_FeedbackReports_Reporter_System]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_FeedbackReports_Reporter_System] ON [dbo].[FeedbackReports]
(
	[ReporterUserId] ASC,
	[SystemFeedbackId] ASC
)
WHERE ([SystemFeedbackId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_InterviewFeedbacks_PublicReviews]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_InterviewFeedbacks_PublicReviews] ON [dbo].[InterviewFeedbacks]
(
	[CreatedAt] DESC,
	[InterviewFeedbackId] ASC
)
INCLUDE([Rating],[QuestionQualityRating],[AiAccuracyRating],[ScoringFairnessRating],[FeedbackUsefulnessRating],[Content]) 
WHERE ([ModerationStatus]=N'Approved' AND [IsVisible]=(1) AND [IsDeleted]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_IntFb_Session]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_IntFb_Session] ON [dbo].[InterviewFeedbacks]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_InterviewFeedbacks_User_Session_Active]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_InterviewFeedbacks_User_Session_Active] ON [dbo].[InterviewFeedbacks]
(
	[UserId] ASC,
	[SessionId] ASC
)
WHERE ([IsDeleted]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SysFb_Category]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_SysFb_Category] ON [dbo].[SystemFeedbacks]
(
	[Category] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SysFb_Status]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_SysFb_Status] ON [dbo].[SystemFeedbacks]
(
	[Status] ASC,
	[IsDeleted] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SysFb_User]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_SysFb_User] ON [dbo].[SystemFeedbacks]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SystemFeedbacks_PublicReviews]    Script Date: 11/08/2026 6:15:26 CH ******/
CREATE NONCLUSTERED INDEX [IX_SystemFeedbacks_PublicReviews] ON [dbo].[SystemFeedbacks]
(
	[CreatedAt] DESC,
	[FeedbackId] ASC
)
INCLUDE([Rating],[UiUxRating],[UsabilityRating],[FeatureQualityRating],[Content],[Category]) 
WHERE ([ModerationStatus]=N'Approved' AND [IsVisible]=(1) AND [IsDeleted]=(0))
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[FeedbackReplies] ADD  DEFAULT (newsequentialid()) FOR [ReplyId]
GO
ALTER TABLE [dbo].[FeedbackReplies] ADD  CONSTRAINT [DF_FeedbackReplies_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[FeedbackReports] ADD  CONSTRAINT [DF_FeedbackReports_FeedbackReportId]  DEFAULT (newsequentialid()) FOR [FeedbackReportId]
GO
ALTER TABLE [dbo].[FeedbackReports] ADD  CONSTRAINT [DF_FeedbackReports_Status]  DEFAULT (N'Pending') FOR [Status]
GO
ALTER TABLE [dbo].[FeedbackReports] ADD  CONSTRAINT [DF_FeedbackReports_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[InterviewFeedbacks] ADD  DEFAULT (newsequentialid()) FOR [InterviewFeedbackId]
GO
ALTER TABLE [dbo].[InterviewFeedbacks] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[InterviewFeedbacks] ADD  CONSTRAINT [DF_InterviewFeedbacks_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[InterviewFeedbacks] ADD  CONSTRAINT [DF_InterviewFeedbacks_ModerationStatus]  DEFAULT (N'Pending') FOR [ModerationStatus]
GO
ALTER TABLE [dbo].[InterviewFeedbacks] ADD  CONSTRAINT [DF_InterviewFeedbacks_IsVisible]  DEFAULT ((0)) FOR [IsVisible]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  DEFAULT (newsequentialid()) FOR [FeedbackId]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  DEFAULT ('General') FOR [Category]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  DEFAULT ('New') FOR [Status]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  CONSTRAINT [DF_SystemFeedbacks_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  CONSTRAINT [DF_SystemFeedbacks_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  CONSTRAINT [DF_SystemFeedbacks_ModerationStatus]  DEFAULT (N'Pending') FOR [ModerationStatus]
GO
ALTER TABLE [dbo].[SystemFeedbacks] ADD  CONSTRAINT [DF_SystemFeedbacks_IsVisible]  DEFAULT ((0)) FOR [IsVisible]
GO
ALTER TABLE [dbo].[FeedbackReplies]  WITH CHECK ADD  CONSTRAINT [FK_Reply_Feedback] FOREIGN KEY([FeedbackId])
REFERENCES [dbo].[SystemFeedbacks] ([FeedbackId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[FeedbackReplies] CHECK CONSTRAINT [FK_Reply_Feedback]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [FK_FeedbackReports_InterviewFeedbacks] FOREIGN KEY([InterviewFeedbackId])
REFERENCES [dbo].[InterviewFeedbacks] ([InterviewFeedbackId])
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [FK_FeedbackReports_InterviewFeedbacks]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [FK_FeedbackReports_SystemFeedbacks] FOREIGN KEY([SystemFeedbackId])
REFERENCES [dbo].[SystemFeedbacks] ([FeedbackId])
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [FK_FeedbackReports_SystemFeedbacks]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [CK_FeedbackReports_ExactlyOneTarget] CHECK  (([InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL OR [InterviewFeedbackId] IS NULL AND [SystemFeedbackId] IS NOT NULL))
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [CK_FeedbackReports_ExactlyOneTarget]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [CK_FeedbackReports_FeedbackType] CHECK  (([FeedbackType]=N'System' OR [FeedbackType]=N'Interview'))
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [CK_FeedbackReports_FeedbackType]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [CK_FeedbackReports_ReasonCode] CHECK  (([ReasonCode]=N'Other' OR [ReasonCode]=N'OffTopicOrIrrelevant' OR [ReasonCode]=N'PrivacyOrPersonalInformation' OR [ReasonCode]=N'SpamOrMisleading' OR [ReasonCode]=N'SexualOrInappropriateContent' OR [ReasonCode]=N'ThreatsOrViolence' OR [ReasonCode]=N'HateOrDiscrimination' OR [ReasonCode]=N'HarassmentOrPersonalAttack'))
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [CK_FeedbackReports_ReasonCode]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [CK_FeedbackReports_Status] CHECK  (([Status]=N'ResolvedByFeedbackDeletion' OR [Status]=N'Dismissed' OR [Status]=N'Pending'))
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [CK_FeedbackReports_Status]
GO
ALTER TABLE [dbo].[FeedbackReports]  WITH CHECK ADD  CONSTRAINT [CK_FeedbackReports_TypeMatchesTarget] CHECK  (([FeedbackType]=N'Interview' AND [InterviewFeedbackId] IS NOT NULL AND [SystemFeedbackId] IS NULL OR [FeedbackType]=N'System' AND [SystemFeedbackId] IS NOT NULL AND [InterviewFeedbackId] IS NULL))
GO
ALTER TABLE [dbo].[FeedbackReports] CHECK CONSTRAINT [CK_FeedbackReports_TypeMatchesTarget]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_AiAccuracyRating_1_5] CHECK  (([AiAccuracyRating] IS NULL OR [AiAccuracyRating]>=(1) AND [AiAccuracyRating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_AiAccuracyRating_1_5]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_FeedbackUsefulnessRating_1_5] CHECK  (([FeedbackUsefulnessRating] IS NULL OR [FeedbackUsefulnessRating]>=(1) AND [FeedbackUsefulnessRating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_FeedbackUsefulnessRating_1_5]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_ModerationStatus] CHECK  (([ModerationStatus]=N'Rejected' OR [ModerationStatus]=N'Approved' OR [ModerationStatus]=N'Pending'))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_ModerationStatus]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_QuestionQualityRating_1_5] CHECK  (([QuestionQualityRating] IS NULL OR [QuestionQualityRating]>=(1) AND [QuestionQualityRating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_QuestionQualityRating_1_5]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_Rating_1_5] CHECK  (([Rating]>=(1) AND [Rating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_Rating_1_5]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_ScoringFairnessRating_1_5] CHECK  (([ScoringFairnessRating] IS NULL OR [ScoringFairnessRating]>=(1) AND [ScoringFairnessRating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_ScoringFairnessRating_1_5]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_InterviewFeedbacks_VisibleOnlyApproved] CHECK  (([IsVisible]=(0) OR [ModerationStatus]=N'Approved'))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_InterviewFeedbacks_VisibleOnlyApproved]
GO
ALTER TABLE [dbo].[InterviewFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_IntFb_Rating] CHECK  (([Rating]>=(1) AND [Rating]<=(5)))
GO
ALTER TABLE [dbo].[InterviewFeedbacks] CHECK CONSTRAINT [CK_IntFb_Rating]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SysFb_Category] CHECK  (([Category]='Other' OR [Category]='Performance' OR [Category]='UI' OR [Category]='FeatureRequest' OR [Category]='Bug' OR [Category]='General'))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SysFb_Category]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SysFb_Rating] CHECK  (([Rating]>=(1) AND [Rating]<=(5)))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SysFb_Rating]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SysFb_Status] CHECK  (([Status]=N'Closed' OR [Status]=N'Resolved' OR [Status]=N'Replied' OR [Status]=N'InReview' OR [Status]=N'New'))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SysFb_Status]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_FeatureQualityRating_1_5] CHECK  (([FeatureQualityRating] IS NULL OR [FeatureQualityRating]>=(1) AND [FeatureQualityRating]<=(5)))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_FeatureQualityRating_1_5]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_ModerationStatus] CHECK  (([ModerationStatus]=N'Rejected' OR [ModerationStatus]=N'Approved' OR [ModerationStatus]=N'Pending'))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_ModerationStatus]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_Rating_1_5] CHECK  (([Rating] IS NULL OR [Rating]>=(1) AND [Rating]<=(5)))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_Rating_1_5]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_UiUxRating_1_5] CHECK  (([UiUxRating] IS NULL OR [UiUxRating]>=(1) AND [UiUxRating]<=(5)))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_UiUxRating_1_5]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_UsabilityRating_1_5] CHECK  (([UsabilityRating] IS NULL OR [UsabilityRating]>=(1) AND [UsabilityRating]<=(5)))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_UsabilityRating_1_5]
GO
ALTER TABLE [dbo].[SystemFeedbacks]  WITH CHECK ADD  CONSTRAINT [CK_SystemFeedbacks_VisibleOnlyApproved] CHECK  (([IsVisible]=(0) OR [ModerationStatus]=N'Approved'))
GO
ALTER TABLE [dbo].[SystemFeedbacks] CHECK CONSTRAINT [CK_SystemFeedbacks_VisibleOnlyApproved]
GO

-- ===================== END OF PASTED CONTENT =================================
