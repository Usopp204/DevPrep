-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Auth   (owned by AuthAPI, source/AuthAPI/Models/AuthDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Auth -> Tasks -> Generate Scripts
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
--   2. NO "USE [DevPrep_Auth]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/01_DevPrep_Auth.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[AccountBlockHistories]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AccountBlockHistories](
	[BlockHistoryId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[Action] [nvarchar](10) NOT NULL,
	[Reason] [nvarchar](500) NULL,
	[PerformedBy] [uniqueidentifier] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_AccountBlockHistories] PRIMARY KEY CLUSTERED 
(
	[BlockHistoryId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Accounts]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Accounts](
	[AccountId] [uniqueidentifier] NOT NULL,
	[Email] [varchar](256) NOT NULL,
	[PasswordHash] [nvarchar](512) NULL,
	[Role] [nvarchar](20) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[IsEmailVerified] [bit] NOT NULL,
	[IsFaceVerified] [bit] NOT NULL,
	[IsVoiceVerified] [bit] NOT NULL,
	[LastLoginAt] [datetime2](7) NULL,
	[FailedLoginCount] [tinyint] NOT NULL,
	[LockedUntil] [datetime2](7) NULL,
	[BlockedReason] [nvarchar](500) NULL,
	[BlockedAt] [datetime2](7) NULL,
	[BlockedBy] [uniqueidentifier] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Accounts] PRIMARY KEY CLUSTERED 
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Accounts_Email] UNIQUE NONCLUSTERED 
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[BiometricVerifications]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[BiometricVerifications](
	[BiometricId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[BiometricType] [nvarchar](10) NOT NULL,
	[ReferenceUrl] [nvarchar](1024) NULL,
	[EmbeddingJson] [nvarchar](max) NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ConfidenceScore] [decimal](5, 4) NULL,
	[VerifiedAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_BiometricVerifications] PRIMARY KEY CLUSTERED 
(
	[BiometricId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EmailVerificationTokens]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EmailVerificationTokens](
	[TokenId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[Token] [nvarchar](256) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[IsUsed] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_EmailVerificationTokens] PRIMARY KEY CLUSTERED 
(
	[TokenId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LoginAudits]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LoginAudits](
	[LoginAuditId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NULL,
	[Email] [nvarchar](256) NULL,
	[IsSuccess] [bit] NOT NULL,
	[Method] [nvarchar](20) NOT NULL,
	[IpAddress] [nvarchar](64) NULL,
	[UserAgent] [nvarchar](512) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_LoginAudits] PRIMARY KEY CLUSTERED 
(
	[LoginAuditId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PasswordResetTokens]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PasswordResetTokens](
	[TokenId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[Token] [nvarchar](256) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[IsUsed] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PasswordResetTokens] PRIMARY KEY CLUSTERED 
(
	[TokenId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RefreshTokens]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RefreshTokens](
	[RefreshTokenId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[Token] [nvarchar](512) NOT NULL,
	[ExpiresAt] [datetime2](7) NOT NULL,
	[IsRevoked] [bit] NOT NULL,
	[RevokedAt] [datetime2](7) NULL,
	[IpAddress] [nvarchar](64) NULL,
	[UserAgent] [nvarchar](512) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_RefreshTokens] PRIMARY KEY CLUSTERED 
(
	[RefreshTokenId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SsoIdentities]    Script Date: 11/08/2026 6:13:28 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SsoIdentities](
	[SsoIdentityId] [uniqueidentifier] NOT NULL,
	[AccountId] [uniqueidentifier] NOT NULL,
	[Provider] [nvarchar](30) NOT NULL,
	[ProviderUserId] [nvarchar](256) NOT NULL,
	[ProviderEmail] [nvarchar](256) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_SsoIdentities] PRIMARY KEY CLUSTERED 
(
	[SsoIdentityId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Sso_Provider] UNIQUE NONCLUSTERED 
(
	[Provider] ASC,
	[ProviderUserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Index [IX_BlockHist_Account]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_BlockHist_Account] ON [dbo].[AccountBlockHistories]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Accounts_Status]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_Accounts_Status] ON [dbo].[Accounts]
(
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Bio_Account]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_Bio_Account] ON [dbo].[BiometricVerifications]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_EmailVer_Token]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_EmailVer_Token] ON [dbo].[EmailVerificationTokens]
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_PwdReset_Token]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_PwdReset_Token] ON [dbo].[PasswordResetTokens]
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Refresh_Account]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_Refresh_Account] ON [dbo].[RefreshTokens]
(
	[AccountId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Refresh_Token]    Script Date: 11/08/2026 6:13:28 CH ******/
CREATE NONCLUSTERED INDEX [IX_Refresh_Token] ON [dbo].[RefreshTokens]
(
	[Token] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AccountBlockHistories] ADD  DEFAULT (newsequentialid()) FOR [BlockHistoryId]
GO
ALTER TABLE [dbo].[AccountBlockHistories] ADD  CONSTRAINT [DF_AccountBlockHistories_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT (newsequentialid()) FOR [AccountId]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT ('User') FOR [Role]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT ((0)) FOR [IsEmailVerified]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT ((0)) FOR [IsFaceVerified]
GO
ALTER TABLE [dbo].[Accounts] ADD  DEFAULT ((0)) FOR [IsVoiceVerified]
GO
ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF_Accounts_FailedLoginCount]  DEFAULT ((0)) FOR [FailedLoginCount]
GO
ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF_Accounts_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF_Accounts_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[BiometricVerifications] ADD  CONSTRAINT [DF__Biometric__Biome__5CD6CB2B]  DEFAULT (newsequentialid()) FOR [BiometricId]
GO
ALTER TABLE [dbo].[BiometricVerifications] ADD  CONSTRAINT [DF__Biometric__Statu__5EBF139D]  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[BiometricVerifications] ADD  CONSTRAINT [DF_BiometricVerifications_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EmailVerificationTokens] ADD  DEFAULT (newsequentialid()) FOR [TokenId]
GO
ALTER TABLE [dbo].[EmailVerificationTokens] ADD  DEFAULT ((0)) FOR [IsUsed]
GO
ALTER TABLE [dbo].[EmailVerificationTokens] ADD  CONSTRAINT [DF_EmailVerificationTokens_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[LoginAudits] ADD  DEFAULT (newsequentialid()) FOR [LoginAuditId]
GO
ALTER TABLE [dbo].[LoginAudits] ADD  DEFAULT ('Password') FOR [Method]
GO
ALTER TABLE [dbo].[LoginAudits] ADD  CONSTRAINT [DF_LoginAudits_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[PasswordResetTokens] ADD  DEFAULT (newsequentialid()) FOR [TokenId]
GO
ALTER TABLE [dbo].[PasswordResetTokens] ADD  DEFAULT ((0)) FOR [IsUsed]
GO
ALTER TABLE [dbo].[PasswordResetTokens] ADD  CONSTRAINT [DF_PasswordResetTokens_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[RefreshTokens] ADD  DEFAULT (newsequentialid()) FOR [RefreshTokenId]
GO
ALTER TABLE [dbo].[RefreshTokens] ADD  DEFAULT ((0)) FOR [IsRevoked]
GO
ALTER TABLE [dbo].[RefreshTokens] ADD  CONSTRAINT [DF_RefreshTokens_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[SsoIdentities] ADD  DEFAULT (newsequentialid()) FOR [SsoIdentityId]
GO
ALTER TABLE [dbo].[SsoIdentities] ADD  CONSTRAINT [DF_SsoIdentities_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[AccountBlockHistories]  WITH CHECK ADD  CONSTRAINT [FK_BlockHist_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AccountBlockHistories] CHECK CONSTRAINT [FK_BlockHist_Account]
GO
ALTER TABLE [dbo].[BiometricVerifications]  WITH CHECK ADD  CONSTRAINT [FK_Bio_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[BiometricVerifications] CHECK CONSTRAINT [FK_Bio_Account]
GO
ALTER TABLE [dbo].[EmailVerificationTokens]  WITH CHECK ADD  CONSTRAINT [FK_EmailVer_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[EmailVerificationTokens] CHECK CONSTRAINT [FK_EmailVer_Account]
GO
ALTER TABLE [dbo].[PasswordResetTokens]  WITH CHECK ADD  CONSTRAINT [FK_PwdReset_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[PasswordResetTokens] CHECK CONSTRAINT [FK_PwdReset_Account]
GO
ALTER TABLE [dbo].[RefreshTokens]  WITH CHECK ADD  CONSTRAINT [FK_Refresh_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[RefreshTokens] CHECK CONSTRAINT [FK_Refresh_Account]
GO
ALTER TABLE [dbo].[SsoIdentities]  WITH CHECK ADD  CONSTRAINT [FK_Sso_Account] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([AccountId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SsoIdentities] CHECK CONSTRAINT [FK_Sso_Account]
GO
ALTER TABLE [dbo].[AccountBlockHistories]  WITH CHECK ADD  CONSTRAINT [CK_BlockHist_Action] CHECK  (([Action]='Unblock' OR [Action]='Block'))
GO
ALTER TABLE [dbo].[AccountBlockHistories] CHECK CONSTRAINT [CK_BlockHist_Action]
GO
ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [CK_Accounts_Role] CHECK  (([Role]='Manager' OR [Role]='Admin' OR [Role]='User'))
GO
ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [CK_Accounts_Role]
GO
ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [CK_Accounts_Status] CHECK  (([Status]='Blocked' OR [Status]='Active' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [CK_Accounts_Status]
GO
ALTER TABLE [dbo].[BiometricVerifications]  WITH CHECK ADD  CONSTRAINT [CK_Bio_Status] CHECK  (([Status]='Failed' OR [Status]='Verified' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[BiometricVerifications] CHECK CONSTRAINT [CK_Bio_Status]
GO
ALTER TABLE [dbo].[BiometricVerifications]  WITH CHECK ADD  CONSTRAINT [CK_Bio_Type] CHECK  (([BiometricType]='Voice' OR [BiometricType]='Face'))
GO
ALTER TABLE [dbo].[BiometricVerifications] CHECK CONSTRAINT [CK_Bio_Type]
GO
ALTER TABLE [dbo].[LoginAudits]  WITH CHECK ADD  CONSTRAINT [CK_Login_Method] CHECK  (([Method]='Admin' OR [Method]='SSO' OR [Method]='Password'))
GO
ALTER TABLE [dbo].[LoginAudits] CHECK CONSTRAINT [CK_Login_Method]
GO
ALTER TABLE [dbo].[SsoIdentities]  WITH CHECK ADD  CONSTRAINT [CK_Sso_Provider] CHECK  (([Provider]='Facebook' OR [Provider]='Microsoft' OR [Provider]='GitHub' OR [Provider]='Google'))
GO
ALTER TABLE [dbo].[SsoIdentities] CHECK CONSTRAINT [CK_Sso_Provider]
GO

-- ===================== END OF PASTED CONTENT =================================
