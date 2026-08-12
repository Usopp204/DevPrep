-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Evaluation   (owned by EvaluationAPI, source/EvaluationAPI/Data/EvaluationDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Evaluation -> Tasks -> Generate Scripts
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
--   2. NO "USE [DevPrep_Evaluation]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/06_DevPrep_Evaluation.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[AnswerEvaluations]    Script Date: 11/08/2026 6:14:54 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AnswerEvaluations](
	[EvaluationId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[SessionQuestionId] [uniqueidentifier] NOT NULL,
	[QuestionContent] [nvarchar](max) NULL,
	[UserAnswer] [nvarchar](max) NULL,
	[Score] [decimal](5, 2) NOT NULL,
	[CriteriaScoresJson] [nvarchar](max) NULL,
	[Strengths] [nvarchar](max) NULL,
	[Weaknesses] [nvarchar](max) NULL,
	[AiFeedback] [nvarchar](max) NULL,
	[ModelUsed] [nvarchar](50) NULL,
	[GeneratedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AnswerEvaluations] PRIMARY KEY CLUSTERED 
(
	[EvaluationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FeedbackReports]    Script Date: 11/08/2026 6:14:54 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FeedbackReports](
	[ReportId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[OverallScore] [decimal](5, 2) NOT NULL,
	[TechnicalScore] [decimal](5, 2) NULL,
	[TheoryScore] [decimal](5, 2) NULL,
	[CommunicationScore] [decimal](5, 2) NULL,
	[ProblemSolvingScore] [decimal](5, 2) NULL,
	[Summary] [nvarchar](max) NULL,
	[Strengths] [nvarchar](max) NULL,
	[Improvements] [nvarchar](max) NULL,
	[Recommendations] [nvarchar](max) NULL,
	[Grade] [nvarchar](5) NULL,
	[GeneratedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_FeedbackReports] PRIMARY KEY CLUSTERED 
(
	[ReportId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_FeedbackReports_Session] UNIQUE NONCLUSTERED 
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PerformanceMetrics]    Script Date: 11/08/2026 6:14:54 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PerformanceMetrics](
	[MetricId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[MetricName] [nvarchar](50) NOT NULL,
	[MetricValue] [decimal](7, 2) NOT NULL,
	[RecordedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PerformanceMetrics] PRIMARY KEY CLUSTERED 
(
	[MetricId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_Eval_Session]    Script Date: 11/08/2026 6:14:54 CH ******/
CREATE NONCLUSTERED INDEX [IX_Eval_Session] ON [dbo].[AnswerEvaluations]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FeedbackReports_SessionId]    Script Date: 11/08/2026 6:14:54 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_FeedbackReports_SessionId] ON [dbo].[FeedbackReports]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Report_Generated]    Script Date: 11/08/2026 6:14:54 CH ******/
CREATE NONCLUSTERED INDEX [IX_Report_Generated] ON [dbo].[FeedbackReports]
(
	[GeneratedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Metric_Session]    Script Date: 11/08/2026 6:14:54 CH ******/
CREATE NONCLUSTERED INDEX [IX_Metric_Session] ON [dbo].[PerformanceMetrics]
(
	[SessionId] ASC,
	[MetricName] ASC,
	[RecordedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AnswerEvaluations] ADD  DEFAULT (newsequentialid()) FOR [EvaluationId]
GO
ALTER TABLE [dbo].[AnswerEvaluations] ADD  CONSTRAINT [DF_AnswerEvaluations_EvaluatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [GeneratedAt]
GO
ALTER TABLE [dbo].[FeedbackReports] ADD  DEFAULT (newsequentialid()) FOR [ReportId]
GO
ALTER TABLE [dbo].[FeedbackReports] ADD  CONSTRAINT [DF_FeedbackReports_GeneratedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [GeneratedAt]
GO
ALTER TABLE [dbo].[PerformanceMetrics] ADD  DEFAULT (newsequentialid()) FOR [MetricId]
GO
ALTER TABLE [dbo].[PerformanceMetrics] ADD  CONSTRAINT [DF_PerformanceMetrics_RecordedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [RecordedAt]
GO

-- ===================== END OF PASTED CONTENT =================================
