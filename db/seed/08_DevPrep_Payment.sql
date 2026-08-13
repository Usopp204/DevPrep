-- SAMPLE-DATA: remove before production =======================================
--
-- Credit packages (so the Buy Credits page is not empty) plus starting credits for
-- the seeded candidate account. Applied by db/init.sh ONLY when
-- SEED_SAMPLE_DATA=true (default false).
--
-- UserId below is the Auth AccountId from 01_DevPrep_Auth.sql — PaymentAPI names
-- the column UserId but there is no separate payment identity. See the note at the
-- top of that file.
-- =============================================================================
SET NOCOUNT ON;
-- REQUIRED, not cosmetic. dbo.CreditLedger carries four filtered indexes
-- (db/baseline/08_DevPrep_Payment.sql:162-200), and SQL Server refuses any INSERT into a
-- table with a filtered index unless QUOTED_IDENTIFIER is ON:
--     Msg 1934, Level 16, State 1 - INSERT failed because the following SET options have
--     incorrect settings: 'QUOTED_IDENTIFIER'.
-- sqlcmd defaults it OFF while SSMS defaults it ON, which is why the INSERT below works
-- when pasted into SSMS by hand but failed through db-init. Every SSMS-generated baseline
-- already emits this line; this file is hand-written, so it has to say it itself.
-- db/init.sh also passes -I as a harness-level guarantee, but keep this line so the file
-- stays correct when run on its own.
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Now datetime2(7) = SYSDATETIME();

-- ---------------------------------------------------------------------------
-- Credit packages
--
-- Priced against the interview cost model in InterviewAPI/Helpers/CreditConfig.cs:
-- 2 credits per minute, so 15/30/45/60-minute sessions cost 30/60/90/120 credits.
--
--   Starter       100 credits   49,000 VND   490 VND/credit   1x 60-min or 3x 15-min
--   Professional  250 + 50      99,000 VND   330 VND/credit   2x 60-min + 1x 30-min
--   Enterprise    600 + 150    199,000 VND   265 VND/credit   6x 60-min
--
-- EXACTLY THREE ACTIVE ROWS IS DELIBERATE. WebClient/Views/Payment/Index.cshtml:9-10
-- takes the first three and only highlights a featured tier when the count is
-- exactly 3 (`featuredIndex = packages.Count == 3 ? 1 : -1`). A fourth active
-- package silently drops off the page; fewer than three loses the highlight.
-- Price per credit decreases across the tiers so each upgrade is a visible gain.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.CreditPackages WHERE PackageId = '5EED0101-0000-4000-8000-000000000101')
BEGIN
    INSERT INTO dbo.CreditPackages
        (PackageId, Name, Description, Credits, BonusCredits, Price, Currency, IsActive, SortOrder, CreatedAt, UpdatedAt)
    VALUES
        ('5EED0101-0000-4000-8000-000000000101', N'Starter',
         N'Try a full-length interview. 100 credits covers one 60-minute session or three 15-minute sessions.',
         100, 0, 49000.00, N'VND', 1, 1, @Now, @Now),

        ('5EED0102-0000-4000-8000-000000000102', N'Professional',
         N'Best value for regular practice. 250 credits plus 50 bonus — enough for two 60-minute sessions and a 30-minute follow-up.',
         250, 50, 99000.00, N'VND', 1, 2, @Now, @Now),

        ('5EED0103-0000-4000-8000-000000000103', N'Enterprise',
         N'Prepare at full intensity. 600 credits plus 150 bonus — six 60-minute interviews with credits to spare.',
         600, 150, 199000.00, N'VND', 1, 3, @Now, @Now);
END;

-- NO `GO` HERE — DELIBERATE. Do not add one back.
-- @Now is declared once near the top of this file. sqlcmd starts a NEW BATCH at
-- every GO, and a variable does not survive a batch boundary, so a GO at this
-- point left the INSERT below in a batch where @Now no longer existed. That is
-- exactly how this file failed once already:
--     Msg 137, Level 15, State 2, Line 65 - Must declare the scalar variable "@Now".
-- (Line 65 was batch-relative, not file-relative, which is why it pointed at a
-- comment when read against the file.) The credit packages above went in fine
-- because they shared the DECLARE's batch; only the ledger row below failed —
-- a partial apply, and the worst kind, since the file looked half-correct.
-- 01_DevPrep_Auth.sql and 02_DevPrep_User.sql keep their DECLARE and every use in
-- a single batch for the same reason. Keep this one that way too.

-- ---------------------------------------------------------------------------
-- Starting credits for the seeded candidate
--
-- EntryType MUST NOT be 'Bonus'. A filtered unique index
-- (PaymentAPI/Sql/001_add_credit_ledger_bonus_index.sql):
--     CREATE UNIQUE INDEX UX_Ledger_UserBonus ON dbo.CreditLedger (UserId)
--         WHERE EntryType = 'Bonus'
-- allows at most ONE Bonus row per user, ever. Seeding a Bonus row would silently
-- consume the one-time 100-credit biometric enrollment award and the candidate
-- could never receive it. 'Refund' has no such constraint.
--
-- EntryType is 'Refund', not 'Purchase'. A 'Purchase' row with a NULL ReferenceId
-- is a combination the application itself never produces — every real purchase is
-- created with an OrderId (see VnPayService.cs:177 and LedgerRepository.cs:40,
-- which only ever looks up a Purchase row by a non-null order id). 'Refund' with a
-- NULL ReferenceId is exactly what PaymentAPI/Program.cs:610-643
-- (POST /admin/credits/add) writes when an admin grants credits by hand — so this
-- seeded row matches a real code path instead of a state the app cannot reach.
--
-- BalanceAfter is a DISPLAY-ONLY running snapshot. The authoritative balance is
-- SUM(CreditChange) — see PaymentAPI/Repositories/LedgerRepository.cs
-- GetBalanceByUserAsync and GetBalanceForUpdateAsync, which both SUM and never
-- read BalanceAfter. It is rendered per row on the Credit History page
-- (WebClient/Views/Profile/CreditHistory.cshtml:293) and in the manager's ledger
-- view, so it still has to be internally consistent or that column reads as
-- nonsense once a real transaction lands on top.
--
-- Exactly ONE seeded row per user keeps that trivially true: it is the first row
-- for this UserId, so the balance before it is 0 and BalanceAfter == CreditChange.
-- If you ever add a second seeded row, its BalanceAfter must be the cumulative
-- SUM including itself.
--
-- ReferenceId is NULL because no Order exists — these credits were not bought.
-- Verified safe: CreditHistory.cshtml:284 guards with `@if (e.ReferenceId is not
-- null)` before formatting it, and HasPurchaseForOrderAsync only matches
-- EntryType = 'Purchase', so a null-reference Refund row cannot collide with it.
--
-- 10,000 credits — sized for repeated testing, not a single run: 83x 60-minute
-- interviews, or 333x 15-minute ones. Verified against the real constraints:
--   * CreditChange and BalanceAfter are [int] (max 2,147,483,647) — no overflow.
--   * The application's own admin grant endpoint (PaymentAPI/Program.cs:617-618)
--     accepts 1..1,000,000 in a single entry, so one 10,000 row is a value the
--     app itself could have produced. It is not bypassing a cap.
--   * Every place the balance is displayed sizes to its content — the navbar and
--     profile pills (.hm-credit-value / .pf-credit-value) are content-width with
--     no max-width, and .stat-value / .cr-balance have no fixed width — so five
--     digits render without truncation. The navbar shows "10,000" via
--     toLocaleString('en-US').
-- Because it stays a single row, BalanceAfter is still provably correct.
--
-- Neither the admin NOR the manager account gets credits: PaymentController:19-20
-- and :32-33, and ProfileController:240-243, redirect Admin AND Manager away from
-- the Buy Credits and Credit History pages, so a balance on those accounts would
-- be data they can never see or spend.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.CreditLedger WHERE LedgerId = '5EED0201-0000-4000-8000-000000000201')
BEGIN
    INSERT INTO dbo.CreditLedger
        (LedgerId, UserId, EntryType, CreditChange, BalanceAfter, ReferenceId, Description, CreatedAt)
    VALUES
        ('5EED0201-0000-4000-8000-000000000201',
         '5EED0002-0000-4000-8000-000000000002',   -- Auth AccountId of candidate@local.devprep.test
         N'Refund', 10000, 10000, NULL,
         N'Sample data - seeded starting credits', @Now);
END;
GO
-- SAMPLE-DATA: end =============================================================
