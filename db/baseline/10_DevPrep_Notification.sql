-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Notification   (owned by NotificationAPI, source/NotificationAPI/Models/NotificationDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Notification -> Tasks -> Generate Scripts
--        -> Select specific database objects: Tables, Views, Stored Procedures,
--           User-Defined Functions, User-Defined Types.
--           Do NOT select Users, Roles, Schemas, or Database Triggers.
--        -> Advanced: see db/README.md section 2 for the exact option values.
--           Configure them ONCE and the output needs no hand-editing.
--
-- HARD RULES FOR THIS FILE
--   1. SCHEMA ONLY, WITH ONE EXCEPTION: this database needs seed rows to boot —
--      email templates — but check first: NotificationAPI/Program.cs:61 seeds VERIFY_EMAIL at startup, so this may be redundant
--      Script those tables as "Schema and data"; everything else schema-only.
--      Still NO user rows, no personal data.
--      shared repository. (Seed/lookup rows are allowed only in the four files
--      that say so: Question, Payment, Notification — see db/README.md.)
--   2. NO "USE [DevPrep_Notification]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/10_DevPrep_Notification.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[Announcements]    Script Date: 11/08/2026 6:18:43 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Announcements](
	[AnnouncementId] [uniqueidentifier] NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[Content] [nvarchar](2000) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeletedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Announcements] PRIMARY KEY CLUSTERED 
(
	[AnnouncementId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EmailLogs]    Script Date: 11/08/2026 6:18:43 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmailLogs](
	[EmailLogId] [uniqueidentifier] NOT NULL,
	[RecipientEmail] [nvarchar](256) NOT NULL,
	[UserId] [uniqueidentifier] NULL,
	[TemplateCode] [nvarchar](50) NULL,
	[Subject] [nvarchar](255) NOT NULL,
	[Body] [nvarchar](max) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ErrorMessage] [nvarchar](max) NULL,
	[RetryCount] [int] NOT NULL,
	[SentAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EmailLogs] PRIMARY KEY CLUSTERED 
(
	[EmailLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EmailTemplates]    Script Date: 11/08/2026 6:18:43 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmailTemplates](
	[TemplateId] [uniqueidentifier] NOT NULL,
	[Code] [nvarchar](50) NOT NULL,
	[Subject] [nvarchar](255) NOT NULL,
	[BodyHtml] [nvarchar](max) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EmailTemplates] PRIMARY KEY CLUSTERED 
(
	[TemplateId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_EmailTemplates_Code] UNIQUE NONCLUSTERED 
(
	[Code] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Index [IX_Announcements_Active]    Script Date: 11/08/2026 6:18:43 CH ******/
CREATE NONCLUSTERED INDEX [IX_Announcements_Active] ON [dbo].[Announcements]
(
	[IsActive] ASC,
	[IsDeleted] ASC,
	[CreatedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Email_Status]    Script Date: 11/08/2026 6:18:43 CH ******/
CREATE NONCLUSTERED INDEX [IX_Email_Status] ON [dbo].[EmailLogs]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Announcements] ADD  DEFAULT (newsequentialid()) FOR [AnnouncementId]
GO
ALTER TABLE [dbo].[Announcements] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Announcements] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[Announcements] ADD  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Announcements] ADD  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[EmailLogs] ADD  DEFAULT (newsequentialid()) FOR [EmailLogId]
GO
ALTER TABLE [dbo].[EmailLogs] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[EmailLogs] ADD  DEFAULT ((0)) FOR [RetryCount]
GO
ALTER TABLE [dbo].[EmailLogs] ADD  CONSTRAINT [DF_EmailLogs_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EmailTemplates] ADD  DEFAULT (newsequentialid()) FOR [TemplateId]
GO
ALTER TABLE [dbo].[EmailTemplates] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[EmailTemplates] ADD  CONSTRAINT [DF_EmailTemplates_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EmailLogs]  WITH CHECK ADD  CONSTRAINT [CK_Email_Status] CHECK  (([Status]='Failed' OR [Status]='Sent' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[EmailLogs] CHECK CONSTRAINT [CK_Email_Status]
GO

-- ===================== END OF PASTED CONTENT =================================
