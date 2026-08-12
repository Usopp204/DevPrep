-- ============================================================================
-- BASELINE SCHEMA — DevPrep_News   (owned by NewsAPI, source/NewsAPI/Models/NewsDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_News -> Tasks -> Generate Scripts
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
--   2. NO "USE [DevPrep_News]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/09_DevPrep_News.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[NewsArticles]    Script Date: 11/08/2026 6:16:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NewsArticles](
	[NewsId] [uniqueidentifier] NOT NULL,
	[CategoryId] [uniqueidentifier] NULL,
	[Title] [nvarchar](255) NOT NULL,
	[Slug] [nvarchar](280) NOT NULL,
	[Summary] [nvarchar](500) NULL,
	[Content] [nvarchar](max) NOT NULL,
	[ThumbnailUrl] [nvarchar](1024) NULL,
	[AuthorId] [uniqueidentifier] NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ViewCount] [int] NOT NULL,
	[IsDeleted] [bit] NOT NULL,
	[DeletedAt] [datetime2](7) NULL,
	[DeletedBy] [uniqueidentifier] NULL,
	[PublishedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_NewsArticles] PRIMARY KEY CLUSTERED 
(
	[NewsId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_NewsArticles_Slug] UNIQUE NONCLUSTERED 
(
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[NewsCategories]    Script Date: 11/08/2026 6:16:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[NewsCategories](
	[CategoryId] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Slug] [nvarchar](120) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_NewsCategories] PRIMARY KEY CLUSTERED 
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_NewsCategories_Slug] UNIQUE NONCLUSTERED 
(
	[Slug] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_News_Category]    Script Date: 11/08/2026 6:16:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_News_Category] ON [dbo].[NewsArticles]
(
	[CategoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_News_Published]    Script Date: 11/08/2026 6:16:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_News_Published] ON [dbo].[NewsArticles]
(
	[PublishedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_News_Status]    Script Date: 11/08/2026 6:16:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_News_Status] ON [dbo].[NewsArticles]
(
	[Status] ASC,
	[IsDeleted] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  DEFAULT (newsequentialid()) FOR [NewsId]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  DEFAULT ('Draft') FOR [Status]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  DEFAULT ((0)) FOR [ViewCount]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  DEFAULT ((0)) FOR [IsDeleted]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  CONSTRAINT [DF_NewsArticles_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[NewsArticles] ADD  CONSTRAINT [DF_NewsArticles_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[NewsCategories] ADD  DEFAULT (newsequentialid()) FOR [CategoryId]
GO
ALTER TABLE [dbo].[NewsCategories] ADD  CONSTRAINT [DF_NewsCategories_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[NewsArticles]  WITH CHECK ADD  CONSTRAINT [FK_News_Category] FOREIGN KEY([CategoryId])
REFERENCES [dbo].[NewsCategories] ([CategoryId])
GO
ALTER TABLE [dbo].[NewsArticles] CHECK CONSTRAINT [FK_News_Category]
GO
ALTER TABLE [dbo].[NewsArticles]  WITH CHECK ADD  CONSTRAINT [CK_News_Status] CHECK  (([Status]='Archived' OR [Status]='Published' OR [Status]='Draft'))
GO
ALTER TABLE [dbo].[NewsArticles] CHECK CONSTRAINT [CK_News_Status]
GO

-- ===================== END OF PASTED CONTENT =================================
