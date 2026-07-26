-- ============================================================================
-- 001_add_credit_ledger_bonus_index.sql — Enforces at most one "Bonus" ledger
-- entry per user (the biometric-enrollment award: 100 credits once, ever, for
-- enrolling both Face and Voice). InterviewAPI checks for an existing Bonus
-- row before crediting, but that check-then-insert alone has a race window
-- under concurrent calls; this unique filtered index is the real server-side
-- guard — a duplicate INSERT violates it and the caller treats that as
-- "already awarded" instead of a hard failure. Idempotent: safe to run
-- multiple times. Run manually in SSMS against the PaymentAPI database
-- (adjust the USE line if your database name differs).
-- ============================================================================
USE [DevPrep_Payment];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Ledger_UserBonus'
      AND object_id = OBJECT_ID(N'dbo.CreditLedger'))
BEGIN
    CREATE UNIQUE INDEX UX_Ledger_UserBonus
        ON dbo.CreditLedger (UserId)
        WHERE EntryType = 'Bonus';
END;
GO