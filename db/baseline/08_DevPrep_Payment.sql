-- ============================================================================
-- BASELINE SCHEMA — DevPrep_Payment   (owned by PaymentAPI, source/PaymentAPI/Data/PaymentDbContext.cs)
-- ============================================================================
-- PLACEHOLDER. Paste SSMS "Generate Scripts" output for this database below.
--
-- HOW TO PRODUCE THE CONTENT
--   SSMS -> right-click DevPrep_Payment -> Tasks -> Generate Scripts
--        -> Select specific database objects: Tables, Views, Stored Procedures,
--           User-Defined Functions, User-Defined Types.
--           Do NOT select Users, Roles, Schemas, or Database Triggers.
--        -> Advanced: see db/README.md section 2 for the exact option values.
--           Configure them ONCE and the output needs no hand-editing.
--
-- HARD RULES FOR THIS FILE
--   1. SCHEMA ONLY, WITH ONE EXCEPTION: this database needs seed rows to boot —
--      credit packages (the Buy Credits page renders empty without them)
--      Script those tables as "Schema and data"; everything else schema-only.
--      Still NO user rows, no personal data.
--      shared repository. (Seed/lookup rows are allowed only in the four files
--      that say so: Question, Payment, Notification — see db/README.md.)
--   2. NO "USE [DevPrep_Payment]" statement. init.sh selects the database with
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
--   grep -vE "^\s*--" db/baseline/08_DevPrep_Payment.sql | grep -nE "USE \[|CREATE DATABASE|ALTER AUTHORIZATION|CREATE USER|GRANT |COLLATE |FILENAME|C:"
--   (the -v strips this header, which necessarily contains those words as documentation)
--
-- ============================================================================


-- ===================== PASTE SSMS OUTPUT BELOW THIS LINE =====================

/****** Object:  Table [dbo].[CreditLedger]    Script Date: 11/08/2026 6:19:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CreditLedger](
	[LedgerId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[EntryType] [nvarchar](20) NOT NULL,
	[CreditChange] [int] NOT NULL,
	[BalanceAfter] [int] NOT NULL,
	[ReferenceId] [uniqueidentifier] NULL,
	[Description] [nvarchar](255) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_CreditLedger] PRIMARY KEY CLUSTERED 
(
	[LedgerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CreditPackages]    Script Date: 11/08/2026 6:19:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CreditPackages](
	[PackageId] [uniqueidentifier] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Credits] [int] NOT NULL,
	[BonusCredits] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[Currency] [nvarchar](10) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_CreditPackages] PRIMARY KEY CLUSTERED 
(
	[PackageId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CreditWallets]    Script Date: 11/08/2026 6:19:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CreditWallets](
	[WalletId] [uniqueidentifier] NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[Balance] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_CreditWallets] PRIMARY KEY CLUSTERED 
(
	[WalletId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_CreditWallets_User] UNIQUE NONCLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Orders]    Script Date: 11/08/2026 6:19:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Orders](
	[OrderId] [uniqueidentifier] NOT NULL,
	[OrderCode] [nvarchar](50) NOT NULL,
	[UserId] [uniqueidentifier] NOT NULL,
	[PackageId] [uniqueidentifier] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[Credits] [int] NOT NULL,
	[Currency] [nvarchar](10) NOT NULL,
	[PaymentMethod] [nvarchar](20) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[ExpiredAt] [datetime2](7) NULL,
	[PaidAt] [datetime2](7) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED 
(
	[OrderId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Orders_Code] UNIQUE NONCLUSTERED 
(
	[OrderCode] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[PaymentTransactions]    Script Date: 11/08/2026 6:19:07 CH ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[PaymentTransactions](
	[TransactionId] [uniqueidentifier] NOT NULL,
	[OrderId] [uniqueidentifier] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[VnpTransactionNo] [nvarchar](50) NULL,
	[VnpBankCode] [nvarchar](20) NULL,
	[VnpResponseCode] [nvarchar](10) NULL,
	[VnpPayDate] [nvarchar](20) NULL,
	[SecureHashValid] [bit] NULL,
	[Status] [nvarchar](20) NOT NULL,
	[RawCallbackJson] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY CLUSTERED 
(
	[TransactionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Index [IX_Ledger_Reference]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Ledger_Reference] ON [dbo].[CreditLedger]
(
	[ReferenceId] ASC
)
WHERE ([ReferenceId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Ledger_User]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Ledger_User] ON [dbo].[CreditLedger]
(
	[UserId] ASC,
	[CreatedAt] ASC
)
INCLUDE([CreditChange]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Ledger_PurchaseOrder]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Ledger_PurchaseOrder] ON [dbo].[CreditLedger]
(
	[ReferenceId] ASC
)
WHERE ([EntryType]='Purchase' AND [ReferenceId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Ledger_RefundPerReference]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Ledger_RefundPerReference] ON [dbo].[CreditLedger]
(
	[UserId] ASC,
	[ReferenceId] ASC
)
WHERE ([EntryType]='Refund' AND [ReferenceId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_Ledger_UserBonus]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_Ledger_UserBonus] ON [dbo].[CreditLedger]
(
	[UserId] ASC
)
WHERE ([EntryType]='Bonus')
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [UX_CreditWallets_UserId]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE UNIQUE NONCLUSTERED INDEX [UX_CreditWallets_UserId] ON [dbo].[CreditWallets]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Order_User]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Order_User] ON [dbo].[Orders]
(
	[UserId] ASC,
	[Status] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Orders_Package]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Orders_Package] ON [dbo].[Orders]
(
	[PackageId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Txn_Order]    Script Date: 11/08/2026 6:19:07 CH ******/
CREATE NONCLUSTERED INDEX [IX_Txn_Order] ON [dbo].[PaymentTransactions]
(
	[OrderId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[CreditLedger] ADD  DEFAULT (newsequentialid()) FOR [LedgerId]
GO
ALTER TABLE [dbo].[CreditLedger] ADD  CONSTRAINT [DF_CreditLedger_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  DEFAULT (newsequentialid()) FOR [PackageId]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  DEFAULT ((0)) FOR [BonusCredits]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  DEFAULT ('VND') FOR [Currency]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  CONSTRAINT [DF_CreditPackages_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[CreditPackages] ADD  CONSTRAINT [DF_CreditPackages_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[CreditWallets] ADD  DEFAULT (newsequentialid()) FOR [WalletId]
GO
ALTER TABLE [dbo].[CreditWallets] ADD  DEFAULT ((0)) FOR [Balance]
GO
ALTER TABLE [dbo].[CreditWallets] ADD  CONSTRAINT [DF_CreditWallets_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[CreditWallets] ADD  CONSTRAINT [DF_CreditWallets_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT (newsequentialid()) FOR [OrderId]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT ('VND') FOR [Currency]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT ('VNPay') FOR [PaymentMethod]
GO
ALTER TABLE [dbo].[Orders] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Orders] ADD  CONSTRAINT [DF_Orders_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Orders] ADD  CONSTRAINT [DF_Orders_UpdatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[PaymentTransactions] ADD  DEFAULT (newsequentialid()) FOR [TransactionId]
GO
ALTER TABLE [dbo].[PaymentTransactions] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[PaymentTransactions] ADD  CONSTRAINT [DF_PaymentTransactions_CreatedAt]  DEFAULT (dateadd(hour,(7),sysutcdatetime())) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [FK_Order_Package] FOREIGN KEY([PackageId])
REFERENCES [dbo].[CreditPackages] ([PackageId])
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [FK_Order_Package]
GO
ALTER TABLE [dbo].[PaymentTransactions]  WITH CHECK ADD  CONSTRAINT [FK_Txn_Order] FOREIGN KEY([OrderId])
REFERENCES [dbo].[Orders] ([OrderId])
GO
ALTER TABLE [dbo].[PaymentTransactions] CHECK CONSTRAINT [FK_Txn_Order]
GO
ALTER TABLE [dbo].[CreditLedger]  WITH CHECK ADD  CONSTRAINT [CK_Ledger_Type] CHECK  (([EntryType]='Adjustment' OR [EntryType]='Bonus' OR [EntryType]='Refund' OR [EntryType]='Usage' OR [EntryType]='Purchase'))
GO
ALTER TABLE [dbo].[CreditLedger] CHECK CONSTRAINT [CK_Ledger_Type]
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [CK_Order_Method] CHECK  (([PaymentMethod]='Manual' OR [PaymentMethod]='Momo' OR [PaymentMethod]='VNPay'))
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [CK_Order_Method]
GO
ALTER TABLE [dbo].[Orders]  WITH CHECK ADD  CONSTRAINT [CK_Order_Status] CHECK  (([Status]='Expired' OR [Status]='Cancelled' OR [Status]='Failed' OR [Status]='Paid' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Orders] CHECK CONSTRAINT [CK_Order_Status]
GO
ALTER TABLE [dbo].[PaymentTransactions]  WITH CHECK ADD  CONSTRAINT [CK_Txn_Status] CHECK  (([Status]='Failed' OR [Status]='Success' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[PaymentTransactions] CHECK CONSTRAINT [CK_Txn_Status]
GO

-- ===================== END OF PASTED CONTENT =================================
