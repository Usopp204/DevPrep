-- ============================================================================
-- BASELINE SCHEMA — DevPrep_User   (owned by UserAPI, source/UserAPI/Models/UserDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_User -> Tasks -> Generate Scripts
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
--   2. NO "USE [DevPrep_User]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/02_DevPrep_User.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[UserProfiles]    Script Date: 11/08/2026 6:20:34 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UserProfiles](
	[UserId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[FullName] [nvarchar](150) NULL,
	[AvatarUrl] [nvarchar](1024) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserProfiles] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_UserProfiles_Account] UNIQUE NONCLUSTERED 
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_User_FullName]    Script Date: 11/08/2026 6:20:34 CH ******/
CREATE NONCLUSTERED INDEX [IX_User_FullName] ON [dbo].[UserProfiles]
(
	[FullName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[UserProfiles] ADD  DEFAULT (newsequentialid()) FOR [UserId]
GO
ALTER TABLE [dbo].[UserProfiles] ADD  CONSTRAINT [DF_UserProfiles_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[UserProfiles] ADD  CONSTRAINT [DF_UserProfiles_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO

-- ===================== END OF PASTED CONTENT =================================
