-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Interview   (owned by InterviewAPI, source/InterviewAPI/Models/InterviewDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Interview -> Tasks -> Generate Scripts
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
--   2. NO "USE [DevPrep_Interview]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/05_DevPrep_Interview.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[ChatMessages]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ChatMessages](
	[MessageId] [uniqueidentifier] NOT NULL,
	[SessionQuestionId] [uniqueidentifier] NOT NULL,
	[SenderType] [nvarchar](10) NOT NULL,
	[MessageType] [nvarchar](20) NOT NULL,
	[Content] [nvarchar](max) NOT NULL,
	[AudioUrl] [nvarchar](1024) NULL,
	[ResponseTimeMs] [int] NULL,
	[SentAt] [datetime2](7) NOT NULL,
	[AnswerReason] [nvarchar](20) NULL,
 CONSTRAINT [PK_ChatMessages] PRIMARY KEY CLUSTERED 
(
	[MessageId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InterviewRooms]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InterviewRooms](
	[RoomId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[ChannelName] [nvarchar](128) NOT NULL,
	[ConnectionId] [nvarchar](128) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[InitializedAt] [datetime2](7) NOT NULL,
	[ClosedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_InterviewRooms] PRIMARY KEY CLUSTERED 
(
	[RoomId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Room_Session] UNIQUE NONCLUSTERED 
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InterviewSessions]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InterviewSessions](
	[SessionId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[CvId] [uniqueidentifier] NULL,
	[JdId] [uniqueidentifier] NULL,
	[DurationMinutes] [int] NOT NULL,
	[Level] [nvarchar](20) NOT NULL,
	[InteractionMode] [nvarchar](20) NOT NULL,
	[QuestionType] [nvarchar](20) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[StartedAt] [datetime2](7) NULL,
	[EndedAt] [datetime2](7) NULL,
	[TotalQuestions] [int] NOT NULL,
	[AnsweredQuestions] [int] NOT NULL,
	[AvgResponseTimeMs] [int] NULL,
	[CreditCost] [int] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeletedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[Position] [nvarchar](100) NULL,
	[TotalPausedSeconds] [int] NOT NULL,
	[PausedAt] [datetime2](7) NULL,
	[PauseCount] [int] NOT NULL,
	[CameraViolationCount] [int] NOT NULL,
	[IntegrityPolicyAccepted] [bit] NOT NULL,
	[IntegrityPolicyAcceptedAt] [datetime2](7) NULL,
	[LastSkipAtUtc] [datetime2](7) NULL,
	[TerminationReason] [nvarchar](32) NULL,
 CONSTRAINT [PK_InterviewSessions] PRIMARY KEY CLUSTERED 
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProfileBiometrics]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProfileBiometrics](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Modality] [nvarchar](16) NOT NULL,
	[Embedding] [varbinary](max) NOT NULL,
	[Dim] [int] NOT NULL,
	[ModelVersion] [nvarchar](128) NOT NULL,
	[Quality] [float] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_ProfileBiometrics] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SessionQuestions]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SessionQuestions](
	[SessionQuestionId] [uniqueidentifier] NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[QuestionId] [uniqueidentifier] NULL,
	[OrderIndex] [int] NOT NULL,
	[Content] [nvarchar](max) NOT NULL,
	[IsAiGenerated] [bit] NOT NULL,
	[ContextSource] [nvarchar](50) NULL,
	[AskedAt] [datetime2](7) NULL,
	[ReferenceAnswer] [nvarchar](max) NULL,
	[HintUsedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_SessionQuestions] PRIMARY KEY CLUSTERED 
(
	[SessionQuestionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SessionVerificationChallenges]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SessionVerificationChallenges](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Modality] [nvarchar](16) NOT NULL,
	[ChallengeJson] [nvarchar](400) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[ConsumedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SessionVerificationChallenges] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SessionVerificationEvents]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SessionVerificationEvents](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[SessionId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Phase] [nvarchar](16) NOT NULL,
	[Modality] [nvarchar](16) NOT NULL,
	[Score] [float] NULL,
	[LivenessPassed] [bit] NULL,
	[Matched] [bit] NOT NULL,
	[Decision] [nvarchar](16) NOT NULL,
	[Detail] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[IncidentId] [uniqueidentifier] NULL,
	[SessionQuestionId] [uniqueidentifier] NULL,
	[ExpiresAt] [datetime2](7) NULL,
	[ConsumedAt] [datetime2](7) NULL,
	[ChallengeJson] [nvarchar](400) NULL,
	[CheckSequence] [int] NULL,
 CONSTRAINT [PK_SessionVerificationEvents] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UserRoomTourStatuses]    Script Date: 11/08/2026 6:15:59 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserRoomTourStatuses](
	[UserId] [uniqueidentifier] NOT NULL,
	[HasSeenRoomTour] [bit] NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_Msg_SessionQuestion]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_Msg_SessionQuestion] ON [dbo].[ChatMessages]
(
	[SessionQuestionId] ASC,
	[SentAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Sess_Status]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_Sess_Status] ON [dbo].[InterviewSessions]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Sess_User]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_Sess_User] ON [dbo].[InterviewSessions]
(
	[UserId] ASC,
	[IsDeleted] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UX_ProfileBiometrics_User_Modality]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_ProfileBiometrics_User_Modality] ON [dbo].[ProfileBiometrics]
(
	[UserId] ASC,
	[Modality] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SQ_Session]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SQ_Session] ON [dbo].[SessionQuestions]
(
	[SessionId] ASC,
	[OrderIndex] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SessionVerificationChallenges_SessionId]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SessionVerificationChallenges_SessionId] ON [dbo].[SessionVerificationChallenges]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SVC_Session_Modality]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SVC_Session_Modality] ON [dbo].[SessionVerificationChallenges]
(
	[SessionId] ASC,
	[Modality] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SessionVerificationEvents_SessionId]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SessionVerificationEvents_SessionId] ON [dbo].[SessionVerificationEvents]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SessionVerificationEvents_SessionQuestionId]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SessionVerificationEvents_SessionQuestionId] ON [dbo].[SessionVerificationEvents]
(
	[SessionQuestionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_SVE_Session_Phase]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE NONCLUSTERED INDEX [IX_SVE_Session_Phase] ON [dbo].[SessionVerificationEvents]
(
	[SessionId] ASC,
	[Phase] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_SVE_IncidentId]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_SVE_IncidentId] ON [dbo].[SessionVerificationEvents]
(
	[IncidentId] ASC
)
WHERE ([IncidentId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_SVE_Session_Check_Sequence]    Script Date: 11/08/2026 6:15:59 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_SVE_Session_Check_Sequence] ON [dbo].[SessionVerificationEvents]
(
	[SessionId] ASC,
	[CheckSequence] ASC
)
WHERE ([CheckSequence] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ChatMessages] ADD  DEFAULT (newsequentialid()) FOR [MessageId]
GO
ALTER TABLE [dbo].[ChatMessages] ADD  DEFAULT ('Text') FOR [MessageType]
GO
ALTER TABLE [dbo].[ChatMessages] ADD  CONSTRAINT [DF_ChatMessages_SentAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [SentAt]
GO
ALTER TABLE [dbo].[InterviewRooms] ADD  DEFAULT (newsequentialid()) FOR [RoomId]
GO
ALTER TABLE [dbo].[InterviewRooms] ADD  DEFAULT ('Open') FOR [Status]
GO
ALTER TABLE [dbo].[InterviewRooms] ADD  CONSTRAINT [DF_InterviewRooms_InitializedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [InitializedAt]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT (newsequentialid()) FOR [SessionId]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((30)) FOR [DurationMinutes]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ('Junior') FOR [Level]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ('Text') FOR [InteractionMode]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ('Mixed') FOR [QuestionType]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ('Configured') FOR [Status]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [TotalQuestions]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [AnsweredQuestions]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [CreditCost]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  CONSTRAINT [DF_InterviewSessions_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  CONSTRAINT [DF_InterviewSessions_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [TotalPausedSeconds]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [PauseCount]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [CameraViolationCount]
GO
ALTER TABLE [dbo].[InterviewSessions] ADD  DEFAULT ((0)) FOR [IntegrityPolicyAccepted]
GO
ALTER TABLE [dbo].[ProfileBiometrics] ADD  CONSTRAINT [DF_ProfileBiometrics_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[ProfileBiometrics] ADD  CONSTRAINT [DF_ProfileBiometrics_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[SessionQuestions] ADD  DEFAULT (newsequentialid()) FOR [SessionQuestionId]
GO
ALTER TABLE [dbo].[SessionQuestions] ADD  DEFAULT ((1)) FOR [IsAiGenerated]
GO
ALTER TABLE [dbo].[SessionVerificationChallenges] ADD  CONSTRAINT [DF_SessionVerificationChallenges_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[SessionVerificationEvents] ADD  CONSTRAINT [DF_SessionVerificationEvents_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[UserRoomTourStatuses] ADD  DEFAULT ((0)) FOR [HasSeenRoomTour]
GO
ALTER TABLE [dbo].[ChatMessages]  WITH CHECK ADD  CONSTRAINT [FK_Msg_SessionQuestion] FOREIGN KEY([SessionQuestionId])
REFERENCES [dbo].[SessionQuestions] ([SessionQuestionId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ChatMessages] CHECK CONSTRAINT [FK_Msg_SessionQuestion]
GO
ALTER TABLE [dbo].[InterviewRooms]  WITH CHECK ADD  CONSTRAINT [FK_Room_Session] FOREIGN KEY([SessionId])
REFERENCES [dbo].[InterviewSessions] ([SessionId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[InterviewRooms] CHECK CONSTRAINT [FK_Room_Session]
GO
ALTER TABLE [dbo].[SessionQuestions]  WITH CHECK ADD  CONSTRAINT [FK_SQ_Session] FOREIGN KEY([SessionId])
REFERENCES [dbo].[InterviewSessions] ([SessionId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SessionQuestions] CHECK CONSTRAINT [FK_SQ_Session]
GO
ALTER TABLE [dbo].[SessionVerificationChallenges]  WITH CHECK ADD  CONSTRAINT [FK_SessionVerificationChallenges_InterviewSessions] FOREIGN KEY([SessionId])
REFERENCES [dbo].[InterviewSessions] ([SessionId])
GO
ALTER TABLE [dbo].[SessionVerificationChallenges] CHECK CONSTRAINT [FK_SessionVerificationChallenges_InterviewSessions]
GO
ALTER TABLE [dbo].[SessionVerificationEvents]  WITH CHECK ADD  CONSTRAINT [FK_SessionVerificationEvents_InterviewSessions] FOREIGN KEY([SessionId])
REFERENCES [dbo].[InterviewSessions] ([SessionId])
GO
ALTER TABLE [dbo].[SessionVerificationEvents] CHECK CONSTRAINT [FK_SessionVerificationEvents_InterviewSessions]
GO
ALTER TABLE [dbo].[SessionVerificationEvents]  WITH CHECK ADD  CONSTRAINT [FK_SessionVerificationEvents_SessionQuestions] FOREIGN KEY([SessionQuestionId])
REFERENCES [dbo].[SessionQuestions] ([SessionQuestionId])
GO
ALTER TABLE [dbo].[SessionVerificationEvents] CHECK CONSTRAINT [FK_SessionVerificationEvents_SessionQuestions]
GO
ALTER TABLE [dbo].[ChatMessages]  WITH CHECK ADD  CONSTRAINT [CK_Msg_Sender] CHECK  (([SenderType]='System' OR [SenderType]='AI' OR [SenderType]='User'))
GO
ALTER TABLE [dbo].[ChatMessages] CHECK CONSTRAINT [CK_Msg_Sender]
GO
ALTER TABLE [dbo].[ChatMessages]  WITH CHECK ADD  CONSTRAINT [CK_Msg_Type] CHECK  (([MessageType]='System' OR [MessageType]='Audio' OR [MessageType]='Answer' OR [MessageType]='Question' OR [MessageType]='Text'))
GO
ALTER TABLE [dbo].[ChatMessages] CHECK CONSTRAINT [CK_Msg_Type]
GO
ALTER TABLE [dbo].[InterviewRooms]  WITH CHECK ADD  CONSTRAINT [CK_Room_Status] CHECK  (([Status]='Closed' OR [Status]='Active' OR [Status]='Open'))
GO
ALTER TABLE [dbo].[InterviewRooms] CHECK CONSTRAINT [CK_Room_Status]
GO
ALTER TABLE [dbo].[InterviewSessions]  WITH CHECK ADD  CONSTRAINT [CK_Sess_Level] CHECK  (([Level]='Lead' OR [Level]='Senior' OR [Level]='Middle' OR [Level]='Junior' OR [Level]='Intern' OR [Level]='Fresher'))
GO
ALTER TABLE [dbo].[InterviewSessions] CHECK CONSTRAINT [CK_Sess_Level]
GO
ALTER TABLE [dbo].[InterviewSessions]  WITH CHECK ADD  CONSTRAINT [CK_Sess_Mode] CHECK  (([InteractionMode]='Video' OR [InteractionMode]='Voice' OR [InteractionMode]='Text'))
GO
ALTER TABLE [dbo].[InterviewSessions] CHECK CONSTRAINT [CK_Sess_Mode]
GO
ALTER TABLE [dbo].[InterviewSessions]  WITH CHECK ADD  CONSTRAINT [CK_Sess_QType] CHECK  (([QuestionType]='Mixed' OR [QuestionType]='Behavioral' OR [QuestionType]='Coding' OR [QuestionType]='Theory'))
GO
ALTER TABLE [dbo].[InterviewSessions] CHECK CONSTRAINT [CK_Sess_QType]
GO
ALTER TABLE [dbo].[InterviewSessions]  WITH CHECK ADD  CONSTRAINT [CK_Sess_Status] CHECK  (([Status]='Cancelled' OR [Status]='Expired' OR [Status]='Completed' OR [Status]='InProgress' OR [Status]='Initialized' OR [Status]='Configured' OR [Status]='Paused'))
GO
ALTER TABLE [dbo].[InterviewSessions] CHECK CONSTRAINT [CK_Sess_Status]
GO

-- ===================== END OF PASTED CONTENT =================================
