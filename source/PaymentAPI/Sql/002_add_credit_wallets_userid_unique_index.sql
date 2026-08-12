-- ============================================================================
-- 002_add_credit_wallets_userid_unique_index.sql — Enforces at most one
-- CreditWallets row per user. InternalCreditService.AwardCreditAsync now
-- creates a zero-balance wallet on demand when a user has never completed a
-- VNPay top-up (previously the only place a wallet was ever created), so the
-- one-time biometric enrollment bonus can actually be awarded to first-time
-- users. That check-then-insert has a race window under concurrent calls;
-- this unique index is the real server-side guard — a duplicate INSERT
-- violates it, and the losing caller re-reads the wallet the winner just
-- created and proceeds normally instead of failing. Idempotent: safe to run
-- multiple times.
--
-- WARNING — run this SELECT first and make sure it returns zero rows. If any
-- UserId already has more than one CreditWallets row, this CREATE UNIQUE
-- INDEX will fail, and more importantly that user's balance is already
-- silently split across multiple rows and needs manual reconciliation before
-- this index can be added:
--
--   SELECT UserId, COUNT(*) AS WalletRowCount, SUM(Balance) AS TotalBalanceAcrossRows
--   FROM dbo.CreditWallets
--   GROUP BY UserId
--   HAVING COUNT(*) > 1;
--
-- Run manually in SSMS against the PaymentAPI database (adjust the USE line
-- if your database name differs).
-- ============================================================================
USE [DevPrep_Payment];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_CreditWallets_UserId'
      AND object_id = OBJECT_ID(N'dbo.CreditWallets'))
BEGIN
    CREATE UNIQUE INDEX UX_CreditWallets_UserId
        ON dbo.CreditWallets (UserId);
END;
GO
