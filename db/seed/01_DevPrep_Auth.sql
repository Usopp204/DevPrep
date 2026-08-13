-- SAMPLE-DATA: remove before production =======================================
--
-- Two sign-in accounts so a fresh clone is usable without manual setup.
-- Applied by db/init.sh ONLY when SEED_SAMPLE_DATA=true (default false).
-- See db/seed/README.md for the password, how to generate the hashes, and how to
-- remove all of this.
--
-- IDENTITY SPANS THREE DATABASES — read this before changing any GUID.
-- Accounts.AccountId here is the SAME value that appears as:
--     DevPrep_User.dbo.UserProfiles.AccountId      (02_DevPrep_User.sql)
--     DevPrep_Payment.dbo.CreditLedger.UserId      (08_DevPrep_Payment.sql)
-- InterviewAPI and PaymentAPI call it "UserId", but there is no separate user
-- identity in this system — it is always the Auth AccountId. Change it in one
-- file and you MUST change it in all three, or the account will sign in with no
-- profile and no credits.
--
-- Every seeded primary key starts with 5EED ("SEED") so one search finds them all:
--     SELECT * FROM dbo.Accounts WHERE CAST(AccountId AS char(36)) LIKE '5EED%';
--
-- Login requires ALL of: Status <> 'Blocked', LockedUntil NULL/past, a BCrypt
-- PasswordHash that verifies, and IsEmailVerified = 1. The admin area additionally
-- requires Role IN ('Admin','Manager'). See AuthAPI/Services/AuthService.cs
-- LoginCoreAsync. IsEmailVerified is set to 1 directly here because the seed does
-- not go through the email-verification flow.
-- =============================================================================
SET NOCOUNT ON;
-- Not required by dbo.Accounts today (it carries no filtered index), but set for the same
-- reason every SSMS-generated baseline sets it: sqlcmd defaults QUOTED_IDENTIFIER OFF and
-- SQL Server then refuses INSERTs into any table with a filtered index (Msg 1934). Adding a
-- filtered index to Accounts later would break this seed with an error that names sqlcmd's
-- defaults rather than the index. See db/seed/08_DevPrep_Payment.sql, where that happened.
SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Password hashes — REPLACE BEFORE USE.
--
-- These placeholders are deliberately NOT valid BCrypt strings, and the guard
-- below aborts the whole seed if they are still here. That is on purpose: a
-- half-applied seed that created accounts nobody can sign into would be worse
-- than a loud failure. db-init runs sqlcmd with -b, so this THROW fails the
-- container and no application service starts.
--
-- Generate both hashes with the command in db/seed/README.md.
-- ---------------------------------------------------------------------------
DECLARE @AdminPasswordHash     nvarchar(512) = N'$2a$12$lCrgoZpj1jLQMEhy3vwsqeSpnx2rJQmJVVVTb6N.Ffm8r9pAWCVYC';
DECLARE @ManagerPasswordHash   nvarchar(512) = N'$2a$12$7X0RXu2.IYwf.AUzXc3FcuTGtMyptyG97CLWpuvi4dlm4SSOqC7Sm';
DECLARE @CandidatePasswordHash nvarchar(512) = N'$2a$12$twoyYEcEL3r.DCiwZzZSq.fEkF2Ua0HqyGSekgCC4ZEMNmCY9K2Ta';

IF @AdminPasswordHash LIKE N'REPLACE_ME%'
   OR @ManagerPasswordHash LIKE N'REPLACE_ME%'
   OR @CandidatePasswordHash LIKE N'REPLACE_ME%'
BEGIN
    THROW 50001,
        'db/seed/01_DevPrep_Auth.sql still contains placeholder password hashes. Generate real BCrypt hashes (see db/seed/README.md) before enabling SEED_SAMPLE_DATA.',
        1;
END;

-- A real BCrypt hash is 60 chars and starts with $2a$/$2b$/$2y$. Catch a paste
-- that lost characters or picked up quotes, rather than creating a dead account.
IF LEN(@AdminPasswordHash) <> 60 OR LEFT(@AdminPasswordHash, 4) <> '$2a$'
   OR LEN(@ManagerPasswordHash) <> 60 OR LEFT(@ManagerPasswordHash, 4) <> '$2a$'
   OR LEN(@CandidatePasswordHash) <> 60 OR LEFT(@CandidatePasswordHash, 4) <> '$2a$'
BEGIN
    THROW 50002,
        'A seeded password hash is not a 60-character $2a$ BCrypt string. Re-generate with the command in db/seed/README.md.',
        1;
END;

DECLARE @Now datetime2(7) = SYSDATETIME();

-- Admin — reaches the admin area (Role = 'Admin'). Gets no credit ledger row:
-- WebClient redirects Admin/Manager away from the interview and payment pages,
-- so credits would be unreachable data. See 08_DevPrep_Payment.sql.
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = '5EED0001-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.Accounts
        (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified,
         IsFaceVerified, IsVoiceVerified, FailedLoginCount, CreatedAt, UpdatedAt)
    VALUES
        ('5EED0001-0000-4000-8000-000000000001',
         N'admin@local.devprep.test',      -- lowercase: LoginCoreAsync lowercases the input before lookup
         @AdminPasswordHash,
         N'Admin', N'Active', 1, 0, 0, 0, @Now, @Now);
END;

-- Manager — reaches the admin area too (LoginCoreAsync accepts Admin OR Manager),
-- but sees the manager-scoped controllers rather than the admin ones.
-- Gets no credit ledger row, for the same reason as Admin: PaymentController:19-20
-- and :32-33 and ProfileController:240-243 redirect Admin AND Manager away from the
-- Buy Credits and Credit History pages, so a balance would be unreachable data.
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = '5EED0003-0000-4000-8000-000000000003')
BEGIN
    INSERT INTO dbo.Accounts
        (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified,
         IsFaceVerified, IsVoiceVerified, FailedLoginCount, CreatedAt, UpdatedAt)
    VALUES
        ('5EED0003-0000-4000-8000-000000000003',
         N'manager@local.devprep.test',
         @ManagerPasswordHash,
         N'Manager', N'Active', 1, 0, 0, 0, @Now, @Now);
END;

-- Candidate — an ordinary interview-taking account, pre-funded in
-- 08_DevPrep_Payment.sql so a demo can start an interview without buying credits.
IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = '5EED0002-0000-4000-8000-000000000002')
BEGIN
    INSERT INTO dbo.Accounts
        (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified,
         IsFaceVerified, IsVoiceVerified, FailedLoginCount, CreatedAt, UpdatedAt)
    VALUES
        ('5EED0002-0000-4000-8000-000000000002',
         N'candidate@local.devprep.test',
         @CandidatePasswordHash,
         N'User', N'Active', 1, 0, 0, 0, @Now, @Now);
END;
GO
-- SAMPLE-DATA: end =============================================================
