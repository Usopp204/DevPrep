-- SAMPLE-DATA: remove before production =======================================
--
-- Profile rows for the two accounts seeded in 01_DevPrep_Auth.sql.
-- Applied by db/init.sh ONLY when SEED_SAMPLE_DATA=true (default false).
--
-- WHY THIS FILE EXISTS: registering through the UI writes to TWO databases —
-- AuthAPI creates the Accounts row, then calls UserAPI
-- (AuthService.PublishAccountRegisteredAsync -> UserProfileService
-- .CreateProfileIfNotExistsAsync) to create the profile. A seeded account with no
-- profile row signs in fine but shows a blank name everywhere a name is resolved,
-- including the admin and manager user lists.
--
-- AccountId below MUST match Accounts.AccountId in 01_DevPrep_Auth.sql — see the
-- identity note at the top of that file. UserId is this table's own primary key
-- and is not used outside DevPrep_User.
-- =============================================================================
SET NOCOUNT ON;
-- Not required by dbo.UserProfiles today (no filtered index), but set for consistency with
-- the other seeds and every SSMS-generated baseline — see 08_DevPrep_Payment.sql for the
-- Msg 1934 failure this prevents once a filtered index exists.
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Now datetime2(7) = SYSDATETIME();

-- UQ_UserProfiles_Account makes AccountId unique, so the guard checks that column
-- rather than the primary key.
IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE AccountId = '5EED0001-0000-4000-8000-000000000001')
BEGIN
    INSERT INTO dbo.UserProfiles (UserId, AccountId, FullName, AvatarUrl, CreatedAt, UpdatedAt)
    VALUES ('5EED0011-0000-4000-8000-000000000011',
            '5EED0001-0000-4000-8000-000000000001',
            N'Local Admin', NULL, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE AccountId = '5EED0003-0000-4000-8000-000000000003')
BEGIN
    INSERT INTO dbo.UserProfiles (UserId, AccountId, FullName, AvatarUrl, CreatedAt, UpdatedAt)
    VALUES ('5EED0013-0000-4000-8000-000000000013',
            '5EED0003-0000-4000-8000-000000000003',
            N'Local Manager', NULL, @Now, @Now);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE AccountId = '5EED0002-0000-4000-8000-000000000002')
BEGIN
    INSERT INTO dbo.UserProfiles (UserId, AccountId, FullName, AvatarUrl, CreatedAt, UpdatedAt)
    VALUES ('5EED0012-0000-4000-8000-000000000012',
            '5EED0002-0000-4000-8000-000000000002',
            N'Local Candidate', NULL, @Now, @Now);
END;
GO
-- SAMPLE-DATA: end =============================================================
