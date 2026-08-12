-- ============================================================================
-- BASELINE SCHEMA — DevPrep_CvJd   (owned by CvJdAPI, source/CvJdAPI/Data/DocumentDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_CvJd -> Tasks -> Generate Scripts
--        -> Select specific database objects: Tables, Views, Stored Procedures,
--           User-Defined Functions, User-Defined Types.
--           Do NOT select Users, Roles, Schemas, or Database Triggers.
--        -> Advanced: see db/README.md section 2 for the exact option values.
--           Configure them ONCE and the output needs no hand-editing.
--
-- ⚠⚠ THIS FILE IS THE ONE THAT CANNOT BE LEFT EMPTY OR PARTIAL ⚠⚠
--
--   CvJdAPI is the only service that self-creates its schema: Program.cs:68 calls
--   db.Database.EnsureCreated() at startup. That API creates the database AND its
--   tables — but ONLY IF THE DATABASE DOES NOT ALREADY EXIST. Against a database
--   that exists but is empty, it returns false and creates nothing.
--
--   init.sh creates DevPrep_CvJd before any service starts. So if this baseline is
--   empty, the sequence is:
--     1. init.sh creates an empty DevPrep_CvJd
--     2. CvJdAPI starts, EnsureCreated() sees it exists, creates no tables
--     3. CvJdAPI reports healthy (/health is a static literal, never touches the DB)
--     4. the first CV or JD request fails with "Invalid object name 'dbo....'"
--
--   A delayed failure that the healthcheck cannot see. Either fill this file
--   completely, or change init.sh to skip DevPrep_CvJd entirely and let
--   EnsureCreated() own it — but not a half-measure between the two.
--   Decision recorded in docs/architecture/schema-recovery.md §4: fill this file.
--
-- HARD RULES FOR THIS FILE
--   1. SCHEMA ONLY. No user rows, no personal data. This file is committed to a
--      shared repository. (Seed/lookup rows are allowed only in the four files
--      that say so: Question, Payment, Notification — see db/README.md.)
--   2. NO "USE [DevPrep_CvJd]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/03_DevPrep_CvJd.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[CvProfiles]    Script Date: 11/08/2026 6:14:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CvProfiles](
	[CvProfileId] [uniqueidentifier] NOT NULL,
	[CvId] [uniqueidentifier] NOT NULL,
	[FullName] [nvarchar](150) NULL,
	[Email] [nvarchar](256) NULL,
	[PhoneNumber] [nvarchar](20) NULL,
	[DateOfBirth] [date] NULL,
	[Gender] [nvarchar](10) NULL,
	[JobTitle] [nvarchar](150) NULL,
	[ExperienceYears] [int] NULL,
	[TargetPosition] [nvarchar](150) NULL,
	[SkillLevel] [nvarchar](20) NULL,
	[Summary] [nvarchar](max) NULL,
	[Location] [nvarchar](150) NULL,
	[LinkedInUrl] [nvarchar](512) NULL,
	[GitHubUrl] [nvarchar](512) NULL,
	[PortfolioUrl] [nvarchar](512) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_CvProfiles] PRIMARY KEY CLUSTERED 
(
	[CvProfileId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_CvProfile_Cv] UNIQUE NONCLUSTERED 
(
	[CvId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Cvs]    Script Date: 11/08/2026 6:14:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Cvs](
	[CvId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[FileName] [nvarchar](255) NOT NULL,
	[FileUrl] [nvarchar](1024) NOT NULL,
	[FileType] [nvarchar](20) NOT NULL,
	[FileSizeKb] [int] NULL,
	[RawText] [nvarchar](max) NULL,
	[IsCurrent] [bit] NOT NULL,
	[ParseStatus] [nvarchar](20) NOT NULL,
	[UploadedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[ParsedJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_Cvs] PRIMARY KEY CLUSTERED 
(
	[CvId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[JobDescriptions]    Script Date: 11/08/2026 6:14:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[JobDescriptions](
	[JdId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Title] [nvarchar](255) NOT NULL,
	[CompanyName] [nvarchar](255) NULL,
	[Position] [nvarchar](150) NULL,
	[Level] [nvarchar](20) NULL,
	[RawText] [nvarchar](max) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[ClarifiedJson] [nvarchar](max) NULL,
 CONSTRAINT [PK_JobDescriptions] PRIMARY KEY CLUSTERED 
(
	[JdId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MatchingScores]    Script Date: 11/08/2026 6:14:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MatchingScores](
	[MatchingScoreId] [uniqueidentifier] NOT NULL,
	[CvId] [uniqueidentifier] NOT NULL,
	[JdId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Score] [decimal](5, 2) NOT NULL,
	[MatchedKeywords] [nvarchar](max) NULL,
	[MissingKeywords] [nvarchar](max) NULL,
	[SuggestionText] [nvarchar](max) NULL,
	[CalculatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_MatchingScores] PRIMARY KEY CLUSTERED 
(
	[MatchingScoreId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Index [IX_CvProfile_Cv]    Script Date: 11/08/2026 6:14:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_CvProfile_Cv] ON [dbo].[CvProfiles]
(
	[CvId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cvs_User]    Script Date: 11/08/2026 6:14:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Cvs_User] ON [dbo].[Cvs]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Cvs_User_Current]    Script Date: 11/08/2026 6:14:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Cvs_User_Current] ON [dbo].[Cvs]
(
	[UserId] ASC,
	[IsCurrent] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Jd_User]    Script Date: 11/08/2026 6:14:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Jd_User] ON [dbo].[JobDescriptions]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Match_User]    Script Date: 11/08/2026 6:14:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Match_User] ON [dbo].[MatchingScores]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[CvProfiles] ADD  DEFAULT (newsequentialid()) FOR [CvProfileId]
GO
ALTER TABLE [dbo].[CvProfiles] ADD  CONSTRAINT [DF_CvProfiles_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CvProfiles] ADD  CONSTRAINT [DF_CvProfiles_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Cvs] ADD  DEFAULT (newsequentialid()) FOR [CvId]
GO
ALTER TABLE [dbo].[Cvs] ADD  DEFAULT ('pdf') FOR [FileType]
GO
ALTER TABLE [dbo].[Cvs] ADD  DEFAULT ((1)) FOR [IsCurrent]
GO
ALTER TABLE [dbo].[Cvs] ADD  DEFAULT ('Pending') FOR [ParseStatus]
GO
ALTER TABLE [dbo].[Cvs] ADD  CONSTRAINT [DF_Cvs_UploadedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UploadedAt]
GO
ALTER TABLE [dbo].[Cvs] ADD  CONSTRAINT [DF_Cvs_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[JobDescriptions] ADD  DEFAULT (newsequentialid()) FOR [JdId]
GO
ALTER TABLE [dbo].[JobDescriptions] ADD  CONSTRAINT [DF_JobDescriptions_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[JobDescriptions] ADD  CONSTRAINT [DF_JobDescriptions_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[MatchingScores] ADD  DEFAULT (newsequentialid()) FOR [MatchingScoreId]
GO
ALTER TABLE [dbo].[MatchingScores] ADD  CONSTRAINT [DF_MatchingScores_CalculatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CalculatedAt]
GO
ALTER TABLE [dbo].[CvProfiles]  WITH CHECK ADD  CONSTRAINT [FK_CvProfile_Cv] FOREIGN KEY([CvId])
REFERENCES [dbo].[Cvs] ([CvId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[CvProfiles] CHECK CONSTRAINT [FK_CvProfile_Cv]
GO
ALTER TABLE [dbo].[MatchingScores]  WITH CHECK ADD  CONSTRAINT [FK_Match_Cv] FOREIGN KEY([CvId])
REFERENCES [dbo].[Cvs] ([CvId])
GO
ALTER TABLE [dbo].[MatchingScores] CHECK CONSTRAINT [FK_Match_Cv]
GO
ALTER TABLE [dbo].[MatchingScores]  WITH CHECK ADD  CONSTRAINT [FK_Match_Jd] FOREIGN KEY([JdId])
REFERENCES [dbo].[JobDescriptions] ([JdId])
GO
ALTER TABLE [dbo].[MatchingScores] CHECK CONSTRAINT [FK_Match_Jd]
GO
ALTER TABLE [dbo].[CvProfiles]  WITH CHECK ADD  CONSTRAINT [CK_CvProfile_Gender] CHECK  (([Gender]='Other' OR [Gender]='Female' OR [Gender]='Male'))
GO
ALTER TABLE [dbo].[CvProfiles] CHECK CONSTRAINT [CK_CvProfile_Gender]
GO
ALTER TABLE [dbo].[CvProfiles]  WITH CHECK ADD  CONSTRAINT [CK_CvProfile_SkillLevel] CHECK  (([SkillLevel]='Lead' OR [SkillLevel]='Senior' OR [SkillLevel]='Middle' OR [SkillLevel]='Junior' OR [SkillLevel]='Intern'))
GO
ALTER TABLE [dbo].[CvProfiles] CHECK CONSTRAINT [CK_CvProfile_SkillLevel]
GO
ALTER TABLE [dbo].[Cvs]  WITH CHECK ADD  CONSTRAINT [CK_Cv_FileType] CHECK  (([FileType]='txt' OR [FileType]='docx' OR [FileType]='doc' OR [FileType]='pdf'))
GO
ALTER TABLE [dbo].[Cvs] CHECK CONSTRAINT [CK_Cv_FileType]
GO
ALTER TABLE [dbo].[Cvs]  WITH CHECK ADD  CONSTRAINT [CK_Cv_ParseStatus] CHECK  (([ParseStatus]='Failed' OR [ParseStatus]='Parsed' OR [ParseStatus]='Pending'))
GO
ALTER TABLE [dbo].[Cvs] CHECK CONSTRAINT [CK_Cv_ParseStatus]
GO
ALTER TABLE [dbo].[JobDescriptions]  WITH CHECK ADD  CONSTRAINT [CK_Jd_Level] CHECK  (([Level]='Lead' OR [Level]='Senior' OR [Level]='Middle' OR [Level]='Junior' OR [Level]='Intern'))
GO
ALTER TABLE [dbo].[JobDescriptions] CHECK CONSTRAINT [CK_Jd_Level]
GO

-- ===================== END OF PASTED CONTENT =================================
