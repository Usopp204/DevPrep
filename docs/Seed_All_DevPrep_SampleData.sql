/*
    DevPrep sample data seed
    - Run once with SQL Server / SSMS.
    - Safe to run again: every INSERT is guarded by IF NOT EXISTS.
    - PasswordHash below is copied from an existing valid BCrypt hash in local sample DB.
*/

/* Shared fixed ids used across databases */
-- Admin:      9A000000-0000-0000-0000-000000000001
-- Manager:    9A000000-0000-0000-0000-000000000002
-- Candidate1: 9A000000-0000-0000-0000-000000000003
-- Candidate2: 9A000000-0000-0000-0000-000000000004

USE [DevPrep_Interview];
GO

IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.ChatMessages', 'AnswerReason') IS NULL
    ALTER TABLE dbo.ChatMessages ADD AnswerReason NVARCHAR(20) NULL;
GO

IF OBJECT_ID('dbo.UserRoomTourStatuses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoomTourStatuses
    (
        UserId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        HasSeenRoomTour BIT NOT NULL DEFAULT 0,
        UpdatedAt DATETIME2 NULL
    );
END
GO

IF OBJECT_ID('dbo.InterviewSessions', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterviewSessions', 'TotalPausedSeconds') IS NULL
    ALTER TABLE dbo.InterviewSessions ADD TotalPausedSeconds INT NOT NULL DEFAULT 0;
GO

IF OBJECT_ID('dbo.InterviewSessions', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterviewSessions', 'PausedAt') IS NULL
    ALTER TABLE dbo.InterviewSessions ADD PausedAt DATETIME2 NULL;
GO

IF OBJECT_ID('dbo.InterviewSessions', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.InterviewSessions', 'PauseCount') IS NULL
    ALTER TABLE dbo.InterviewSessions ADD PauseCount INT NOT NULL DEFAULT 0;
GO

USE [DevPrep_Auth];
GO

IF OBJECT_ID('dbo.Accounts', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.EmailVerificationTokens', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.PasswordResetTokens', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.RefreshTokens', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.LoginAudits', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.BiometricVerifications', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.SsoIdentities', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.AccountBlockHistories', 'U') IS NOT NULL
BEGIN
DECLARE @PasswordHash nvarchar(1024) = N'$2a$11$hV72l142rG.u4D7KEuCgC.YBs4690/.Emh1p8q1q41R8aG8B4.Oo6';
DECLARE @AdminId uniqueidentifier = '9A000000-0000-0000-0000-000000000001';
DECLARE @ManagerId uniqueidentifier = '9A000000-0000-0000-0000-000000000002';
DECLARE @User1Id uniqueidentifier = '9A000000-0000-0000-0000-000000000003';
DECLARE @User2Id uniqueidentifier = '9A000000-0000-0000-0000-000000000004';

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = @AdminId)
INSERT dbo.Accounts (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified, IsFaceVerified, IsVoiceVerified, LastLoginAt, FailedLoginCount, LockedUntil, BlockedReason, BlockedAt, BlockedBy, CreatedAt, UpdatedAt)
VALUES (@AdminId, 'seed.admin@devprep.local', @PasswordHash, N'Admin', N'Active', 1, 1, 1, DATEADD(day, -1, SYSUTCDATETIME()), 0, NULL, NULL, NULL, NULL, DATEADD(day, -30, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = @ManagerId)
INSERT dbo.Accounts (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified, IsFaceVerified, IsVoiceVerified, LastLoginAt, FailedLoginCount, LockedUntil, BlockedReason, BlockedAt, BlockedBy, CreatedAt, UpdatedAt)
VALUES (@ManagerId, 'seed.manager@devprep.local', @PasswordHash, N'Manager', N'Active', 1, 1, 1, DATEADD(hour, -8, SYSUTCDATETIME()), 0, NULL, NULL, NULL, NULL, DATEADD(day, -25, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = @User1Id)
INSERT dbo.Accounts (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified, IsFaceVerified, IsVoiceVerified, LastLoginAt, FailedLoginCount, LockedUntil, BlockedReason, BlockedAt, BlockedBy, CreatedAt, UpdatedAt)
VALUES (@User1Id, 'seed.an.nguyen@devprep.local', @PasswordHash, N'User', N'Active', 1, 1, 0, DATEADD(hour, -2, SYSUTCDATETIME()), 0, NULL, NULL, NULL, NULL, DATEADD(day, -20, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Accounts WHERE AccountId = @User2Id)
INSERT dbo.Accounts (AccountId, Email, PasswordHash, Role, Status, IsEmailVerified, IsFaceVerified, IsVoiceVerified, LastLoginAt, FailedLoginCount, LockedUntil, BlockedReason, BlockedAt, BlockedBy, CreatedAt, UpdatedAt)
VALUES (@User2Id, 'seed.linh.pham@devprep.local', @PasswordHash, N'User', N'Active', 1, 0, 0, DATEADD(day, -3, SYSUTCDATETIME()), 0, NULL, NULL, NULL, NULL, DATEADD(day, -18, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.EmailVerificationTokens WHERE TokenId = '9A100000-0000-0000-0000-000000000001')
INSERT dbo.EmailVerificationTokens (TokenId, AccountId, Token, ExpiresAt, IsUsed, CreatedAt)
VALUES ('9A100000-0000-0000-0000-000000000001', @User1Id, N'seed-email-token-an', DATEADD(day, 1, SYSUTCDATETIME()), 0, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.PasswordResetTokens WHERE TokenId = '9A110000-0000-0000-0000-000000000001')
INSERT dbo.PasswordResetTokens (TokenId, AccountId, Token, ExpiresAt, IsUsed, CreatedAt)
VALUES ('9A110000-0000-0000-0000-000000000001', @User2Id, N'seed-reset-token-linh', DATEADD(hour, 2, SYSUTCDATETIME()), 0, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.RefreshTokens WHERE RefreshTokenId = '9A120000-0000-0000-0000-000000000001')
INSERT dbo.RefreshTokens (RefreshTokenId, AccountId, Token, ExpiresAt, IsRevoked, RevokedAt, IpAddress, UserAgent, CreatedAt)
VALUES ('9A120000-0000-0000-0000-000000000001', @ManagerId, N'seed-refresh-token-manager', DATEADD(day, 7, SYSUTCDATETIME()), 0, NULL, N'127.0.0.1', N'DevPrep Seed Browser', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.LoginAudits WHERE LoginAuditId = '9A130000-0000-0000-0000-000000000001')
INSERT dbo.LoginAudits (LoginAuditId, AccountId, Email, IsSuccess, Method, IpAddress, UserAgent, CreatedAt)
VALUES ('9A130000-0000-0000-0000-000000000001', @ManagerId, N'seed.manager@devprep.local', 1, N'Password', N'127.0.0.1', N'DevPrep Seed Browser', DATEADD(hour, -8, SYSUTCDATETIME()));

IF NOT EXISTS (SELECT 1 FROM dbo.BiometricVerifications WHERE BiometricId = '9A140000-0000-0000-0000-000000000001')
INSERT dbo.BiometricVerifications (BiometricId, AccountId, BiometricType, ReferenceUrl, EmbeddingJson, Status, ConfidenceScore, VerifiedAt, CreatedAt)
VALUES ('9A140000-0000-0000-0000-000000000001', @User1Id, N'Face', N'https://cdn.devprep.local/biometrics/an-face.jpg', N'{"vector":[0.12,0.34,0.56]}', N'Verified', 0.9620, SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.SsoIdentities WHERE SsoIdentityId = '9A150000-0000-0000-0000-000000000001')
INSERT dbo.SsoIdentities (SsoIdentityId, AccountId, Provider, ProviderUserId, ProviderEmail, CreatedAt)
VALUES ('9A150000-0000-0000-0000-000000000001', @User2Id, N'Google', N'google-seed-linh-001', N'seed.linh.pham@gmail.com', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.AccountBlockHistories WHERE BlockHistoryId = '9A160000-0000-0000-0000-000000000001')
INSERT dbo.AccountBlockHistories (BlockHistoryId, AccountId, Action, Reason, PerformedBy, CreatedAt)
VALUES ('9A160000-0000-0000-0000-000000000001', @User2Id, N'Unblock', N'User confirmed identity and account is active for demo data.', @AdminId, DATEADD(day, -5, SYSUTCDATETIME()));
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Auth seed because one or more required tables are missing.';
END
GO

USE [DevPrep_User];
GO

IF OBJECT_ID('dbo.UserProfiles', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = '9A000000-0000-0000-0000-000000000003')
INSERT dbo.UserProfiles (UserId, AccountId, FullName, AvatarUrl, CreatedAt, UpdatedAt)
VALUES ('9A000000-0000-0000-0000-000000000003', '9A000000-0000-0000-0000-000000000003', N'An Nguyen', N'https://cdn.devprep.local/avatars/an-nguyen.png', DATEADD(day, -20, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = '9A000000-0000-0000-0000-000000000004')
INSERT dbo.UserProfiles (UserId, AccountId, FullName, AvatarUrl, CreatedAt, UpdatedAt)
VALUES ('9A000000-0000-0000-0000-000000000004', '9A000000-0000-0000-0000-000000000004', N'Linh Pham', N'https://cdn.devprep.local/avatars/linh-pham.png', DATEADD(day, -18, SYSUTCDATETIME()), SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_User seed because dbo.UserProfiles is missing.';
END
GO

USE [DevPrep_Question];
GO

IF OBJECT_ID('dbo.QuestionCategories', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Questions', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.QuestionCategories WHERE CategoryId = '9A200000-0000-0000-0000-000000000001')
INSERT dbo.QuestionCategories (CategoryId, Name, Description, IsActive, CreatedAt)
VALUES ('9A200000-0000-0000-0000-000000000001', N'Backend .NET', N'Questions for ASP.NET Core API, EF Core, SQL Server, and service design.', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.QuestionCategories WHERE CategoryId = '9A200000-0000-0000-0000-000000000002')
INSERT dbo.QuestionCategories (CategoryId, Name, Description, IsActive, CreatedAt)
VALUES ('9A200000-0000-0000-0000-000000000002', N'Frontend React', N'Questions for React, JavaScript, UI state, and API integration.', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Questions WHERE QuestionId = '9A210000-0000-0000-0000-000000000001')
INSERT dbo.Questions (QuestionId, CategoryId, Content, QuestionType, Level, Difficulty, ExpectedAnswer, TechStack, Tags, Status, IsDeleted, DeletedAt, CreatedBy, CreatedAt, UpdatedAt, DeletedBy)
VALUES ('9A210000-0000-0000-0000-000000000001', '9A200000-0000-0000-0000-000000000001', N'Explain how dependency injection works in ASP.NET Core and when you would use Scoped lifetime.', N'Theory', N'Middle', N'Medium', N'Candidate should explain container registration, constructor injection, request scope, and DbContext as a common scoped dependency.', N'.NET, ASP.NET Core', N'di,service-lifetime,backend', N'Active', 0, NULL, '9A000000-0000-0000-0000-000000000002', SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.Questions WHERE QuestionId = '9A210000-0000-0000-0000-000000000002')
INSERT dbo.Questions (QuestionId, CategoryId, Content, QuestionType, Level, Difficulty, ExpectedAnswer, TechStack, Tags, Status, IsDeleted, DeletedAt, CreatedBy, CreatedAt, UpdatedAt, DeletedBy)
VALUES ('9A210000-0000-0000-0000-000000000002', '9A200000-0000-0000-0000-000000000002', N'How would you prevent unnecessary re-rendering in a React list page that receives search and filter input?', N'Situational', N'Middle', N'Medium', N'Candidate should mention memoization, stable keys, debouncing search input, splitting components, and avoiding derived state mistakes.', N'React, JavaScript', N'react,performance,search', N'Active', 0, NULL, '9A000000-0000-0000-0000-000000000002', SYSUTCDATETIME(), SYSUTCDATETIME(), NULL);
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Question seed because one or more required tables are missing.';
END
GO

USE [DevPrep_CvJd];
GO

IF OBJECT_ID('dbo.Cvs', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.JobDescriptions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.CvProfiles', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.CvKeywords', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.JdKeywords', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.MatchingScores', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.Cvs WHERE CvId = '9A300000-0000-0000-0000-000000000001')
INSERT dbo.Cvs (CvId, UserId, FileName, FileUrl, FileType, FileSizeKb, RawText, IsCurrent, ParseStatus, UploadedAt, UpdatedAt, ParsedJson)
VALUES ('9A300000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', N'an-nguyen-backend-cv.pdf', N'https://cdn.devprep.local/cv/an-nguyen-backend-cv.pdf', N'pdf', 428, N'An Nguyen - Backend Developer with 3 years of ASP.NET Core, EF Core, SQL Server and REST API experience.', 1, N'Parsed', DATEADD(day, -12, SYSUTCDATETIME()), SYSUTCDATETIME(), N'{"fullName":"An Nguyen","skills":["ASP.NET Core","EF Core","SQL Server"]}');

IF NOT EXISTS (SELECT 1 FROM dbo.JobDescriptions WHERE JdId = '9A310000-0000-0000-0000-000000000001')
INSERT dbo.JobDescriptions (JdId, UserId, Title, CompanyName, Position, Level, RawText, CreatedAt, UpdatedAt, ClarifiedJson)
VALUES ('9A310000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', N'Middle Backend Developer', N'DevPrep Labs', N'Backend Developer', N'Middle', N'Build REST APIs using ASP.NET Core, SQL Server, EF Core, authentication, logging and clean service layers.', DATEADD(day, -11, SYSUTCDATETIME()), SYSUTCDATETIME(), N'{"position":"Backend Developer","level":"Middle","requiredSkills":["ASP.NET Core","SQL Server","EF Core"]}');

IF NOT EXISTS (SELECT 1 FROM dbo.CvProfiles WHERE CvProfileId = '9A320000-0000-0000-0000-000000000001')
INSERT dbo.CvProfiles (CvProfileId, CvId, FullName, Email, PhoneNumber, DateOfBirth, Gender, JobTitle, ExperienceYears, TargetPosition, SkillLevel, Summary, Location, LinkedInUrl, GitHubUrl, PortfolioUrl, CreatedAt, UpdatedAt)
VALUES ('9A320000-0000-0000-0000-000000000001', '9A300000-0000-0000-0000-000000000001', N'An Nguyen', N'seed.an.nguyen@devprep.local', N'0901000001', '1999-02-14', N'Male', N'Backend Developer', 3, N'Middle Backend Developer', N'Middle', N'Focused on API design, SQL performance, and service reliability.', N'Ho Chi Minh City', N'https://linkedin.com/in/seed-an-nguyen', N'https://github.com/seed-an-nguyen', N'https://portfolio.devprep.local/an', SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.CvKeywords WHERE CvKeywordId = '9A330000-0000-0000-0000-000000000001')
INSERT dbo.CvKeywords (CvKeywordId, CvId, Keyword, Category, Weight, ExtractedAt)
VALUES ('9A330000-0000-0000-0000-000000000001', '9A300000-0000-0000-0000-000000000001', N'ASP.NET Core', N'Framework', 0.9500, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.JdKeywords WHERE JdKeywordId = '9A340000-0000-0000-0000-000000000001')
INSERT dbo.JdKeywords (JdKeywordId, JdId, Keyword, Category, IsRequired, Weight, ExtractedAt)
VALUES ('9A340000-0000-0000-0000-000000000001', '9A310000-0000-0000-0000-000000000001', N'EF Core', N'Framework', 1, 0.9000, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.MatchingScores WHERE MatchingScoreId = '9A350000-0000-0000-0000-000000000001')
INSERT dbo.MatchingScores (MatchingScoreId, CvId, JdId, UserId, Score, MatchedKeywords, MissingKeywords, SuggestionText, CalculatedAt)
VALUES ('9A350000-0000-0000-0000-000000000001', '9A300000-0000-0000-0000-000000000001', '9A310000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', 86.50, N'ASP.NET Core, EF Core, SQL Server', N'Message queue, Redis', N'Prepare more examples about background jobs and caching for the interview.', SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_CvJd seed because one or more required tables are missing.';
END
GO

USE [DevPrep_Interview];
GO

IF OBJECT_ID('dbo.InterviewSessions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.SessionQuestions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.InterviewRooms', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.UserRoomTourStatuses', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.InterviewSessions WHERE SessionId = '9A400000-0000-0000-0000-000000000001')
INSERT dbo.InterviewSessions (SessionId, UserId, CvId, JdId, Title, DurationMinutes, Level, InteractionMode, QuestionType, Status, StartedAt, EndedAt, TotalQuestions, AnsweredQuestions, AvgResponseTimeMs, CreditCost, IsDeleted, DeletedAt, CreatedAt, UpdatedAt, Position, TotalPausedSeconds, PausedAt, PauseCount)
VALUES ('9A400000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', '9A300000-0000-0000-0000-000000000001', '9A310000-0000-0000-0000-000000000001', N'Backend .NET practice interview', 30, N'Middle', N'Text', N'Mixed', N'Completed', DATEADD(day, -2, SYSUTCDATETIME()), DATEADD(day, -2, DATEADD(minute, 28, SYSUTCDATETIME())), 2, 2, 4200, 5, 0, NULL, DATEADD(day, -2, SYSUTCDATETIME()), SYSUTCDATETIME(), N'Backend Developer', 75, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SessionQuestions WHERE SessionQuestionId = '9A410000-0000-0000-0000-000000000001')
INSERT dbo.SessionQuestions (SessionQuestionId, SessionId, QuestionId, OrderIndex, Content, IsAiGenerated, ContextSource, AskedAt, ReferenceAnswer)
VALUES ('9A410000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', '9A210000-0000-0000-0000-000000000001', 1, N'Explain Scoped lifetime in ASP.NET Core dependency injection.', 0, N'QuestionBank', DATEADD(day, -2, SYSUTCDATETIME()), N'Scoped services are created once per request and are suitable for DbContext.');

IF NOT EXISTS (SELECT 1 FROM dbo.ChatMessages WHERE MessageId = '9A420000-0000-0000-0000-000000000001')
INSERT dbo.ChatMessages (MessageId, SessionQuestionId, SenderType, MessageType, Content, AudioUrl, ResponseTimeMs, SentAt, AnswerReason)
VALUES ('9A420000-0000-0000-0000-000000000001', '9A410000-0000-0000-0000-000000000001', N'User', N'Text', N'Scoped lifetime creates one service instance for each HTTP request. I usually use it for EF Core DbContext because all repositories in the same request should share the same unit of work.', NULL, 4100, DATEADD(day, -2, DATEADD(second, 45, SYSUTCDATETIME())), N'Answered');

IF NOT EXISTS (SELECT 1 FROM dbo.InterviewRooms WHERE RoomId = '9A430000-0000-0000-0000-000000000001')
INSERT dbo.InterviewRooms (RoomId, SessionId, ChannelName, ConnectionId, Status, InitializedAt, ClosedAt)
VALUES ('9A430000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', N'room-backend-seed-001', N'signalr-seed-conn-001', N'Closed', DATEADD(day, -2, SYSUTCDATETIME()), DATEADD(day, -2, DATEADD(minute, 29, SYSUTCDATETIME())));

IF NOT EXISTS (SELECT 1 FROM dbo.UserRoomTourStatuses WHERE UserId = '9A000000-0000-0000-0000-000000000003')
INSERT dbo.UserRoomTourStatuses (UserId, HasSeenRoomTour, UpdatedAt)
VALUES ('9A000000-0000-0000-0000-000000000003', 1, SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Interview seed because one or more required tables are missing. Run the interview schema/migration first, then run this seed again.';
END
GO

USE [DevPrep_Evaluation];
GO

IF OBJECT_ID('dbo.AnswerEvaluations', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.FeedbackReports', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.PerformanceMetrics', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.AnswerEvaluations WHERE EvaluationId = '9A500000-0000-0000-0000-000000000001')
INSERT dbo.AnswerEvaluations (EvaluationId, SessionId, SessionQuestionId, QuestionContent, UserAnswer, Score, CriteriaScoresJson, Strengths, Weaknesses, AiFeedback, ModelUsed, GeneratedAt)
VALUES ('9A500000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', '9A410000-0000-0000-0000-000000000001', N'Explain Scoped lifetime in ASP.NET Core dependency injection.', N'Scoped lifetime creates one service instance for each HTTP request...', 8.50, N'{"accuracy":8.5,"clarity":8.0,"example":9.0}', N'Clear example with DbContext and repository usage.', N'Could mention background services and scope creation.', N'Good practical answer. Add one warning about using scoped services from singleton services.', N'gpt-4.1-mini', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.FeedbackReports WHERE ReportId = '9A510000-0000-0000-0000-000000000001')
INSERT dbo.FeedbackReports (ReportId, SessionId, OverallScore, TechnicalScore, TheoryScore, CommunicationScore, ProblemSolvingScore, Summary, Strengths, Improvements, Recommendations, Grade, GeneratedAt)
VALUES ('9A510000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', 8.20, 8.50, 8.00, 7.80, 8.30, N'Candidate shows solid backend fundamentals and practical ASP.NET Core experience.', N'Dependency injection, EF Core, API layering.', N'Add more examples about caching and async failure handling.', N'Review Redis caching, message queues, and integration testing.', N'B+', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.PerformanceMetrics WHERE MetricId = '9A520000-0000-0000-0000-000000000001')
INSERT dbo.PerformanceMetrics (MetricId, SessionId, MetricName, MetricValue, RecordedAt)
VALUES ('9A520000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', N'AverageAnswerLatencySeconds', 4.20, SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Evaluation seed because one or more required tables are missing.';
END
GO

USE [DevPrep_Feedback];
GO

IF OBJECT_ID('dbo.InterviewFeedbacks', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.SystemFeedbacks', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.FeedbackReplies', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.InterviewFeedbacks WHERE InterviewFeedbackId = '9A600000-0000-0000-0000-000000000001')
INSERT dbo.InterviewFeedbacks (InterviewFeedbackId, SessionId, UserId, Rating, Content, IsHelpful, IsDeleted, DeletedAt, CreatedAt)
VALUES ('9A600000-0000-0000-0000-000000000001', '9A400000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', 5, N'AI interviewer asked relevant backend questions and the final feedback was useful.', 1, 0, NULL, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.SystemFeedbacks WHERE FeedbackId = '9A610000-0000-0000-0000-000000000001')
INSERT dbo.SystemFeedbacks (FeedbackId, UserId, Category, Subject, Content, Rating, Status, IsDeleted, DeletedAt, CreatedAt, UpdatedAt)
VALUES ('9A610000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', N'Performance', N'Interview result page loads slowly', N'The report page took several seconds to load after the interview ended.', 3, N'New', 0, NULL, DATEADD(day, -1, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.FeedbackReplies WHERE ReplyId = '9A620000-0000-0000-0000-000000000001')
INSERT dbo.FeedbackReplies (ReplyId, FeedbackId, RepliedBy, ReplyContent, CreatedAt)
VALUES ('9A620000-0000-0000-0000-000000000001', '9A610000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000002', N'Thanks for reporting. The team is checking report aggregation latency.', SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Feedback seed because one or more required tables are missing.';
END
GO

USE [DevPrep_News];
GO

IF OBJECT_ID('dbo.NewsCategories', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.NewsArticles', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.NewsCategories WHERE CategoryId = '9A700000-0000-0000-0000-000000000001')
INSERT dbo.NewsCategories (CategoryId, Name, Slug, CreatedAt)
VALUES ('9A700000-0000-0000-0000-000000000001', N'Product Updates', N'product-updates', SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.NewsArticles WHERE NewsId = '9A710000-0000-0000-0000-000000000001')
INSERT dbo.NewsArticles (NewsId, CategoryId, Title, Slug, Summary, Content, ThumbnailUrl, AuthorId, Status, ViewCount, IsDeleted, DeletedAt, DeletedBy, PublishedAt, CreatedAt, UpdatedAt)
VALUES ('9A710000-0000-0000-0000-000000000001', '9A700000-0000-0000-0000-000000000001', N'DevPrep adds manager read-only dashboards', N'devprep-adds-manager-read-only-dashboards', N'Managers can now review users, questions, feedback, payments, and interview sessions without editing production data.', N'<h2>Manager dashboards</h2><p>This update adds read-only monitoring screens for managers, including feedback quality, payment volume, interview sessions, and news content.</p>', N'https://cdn.devprep.local/news/manager-dashboard.png', '9A000000-0000-0000-0000-000000000002', N'Published', 128, 0, NULL, NULL, DATEADD(day, -1, SYSUTCDATETIME()), DATEADD(day, -1, SYSUTCDATETIME()), SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_News seed because one or more required tables are missing.';
END
GO

USE [DevPrep_Payment];
GO

IF OBJECT_ID('dbo.CreditPackages', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.CreditWallets', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Orders', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.PaymentTransactions', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.CreditLedger', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.CreditPackages WHERE PackageId = '9A800000-0000-0000-0000-000000000001')
INSERT dbo.CreditPackages (PackageId, Name, Description, Credits, BonusCredits, Price, Currency, IsActive, SortOrder, CreatedAt, UpdatedAt)
VALUES ('9A800000-0000-0000-0000-000000000001', N'Starter Interview Pack', N'Best for candidates who want to try a few mock interviews.', 20, 2, 99000.00, N'VND', 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.CreditWallets WHERE WalletId = '9A810000-0000-0000-0000-000000000001')
INSERT dbo.CreditWallets (WalletId, UserId, Balance, UpdatedAt, CreatedAt)
VALUES ('9A810000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', 17, SYSUTCDATETIME(), DATEADD(day, -12, SYSUTCDATETIME()));

IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE OrderId = '9A820000-0000-0000-0000-000000000001')
INSERT dbo.Orders (OrderId, OrderCode, UserId, PackageId, Amount, Credits, Currency, PaymentMethod, Status, ExpiredAt, PaidAt, CreatedAt, UpdatedAt)
VALUES ('9A820000-0000-0000-0000-000000000001', N'DP-SEED-ORDER-001', '9A000000-0000-0000-0000-000000000003', '9A800000-0000-0000-0000-000000000001', 99000.00, 22, N'VND', N'VNPAY', N'Paid', DATEADD(hour, 1, SYSUTCDATETIME()), DATEADD(day, -10, SYSUTCDATETIME()), DATEADD(day, -10, SYSUTCDATETIME()), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.PaymentTransactions WHERE TransactionId = '9A830000-0000-0000-0000-000000000001')
INSERT dbo.PaymentTransactions (TransactionId, OrderId, Amount, VnpTransactionNo, VnpBankCode, VnpResponseCode, VnpPayDate, SecureHashValid, Status, RawCallbackJson, CreatedAt)
VALUES ('9A830000-0000-0000-0000-000000000001', '9A820000-0000-0000-0000-000000000001', 99000.00, N'VNP-SEED-20260707001', N'NCB', N'00', N'20260707101010', 1, N'Success', N'{"vnp_ResponseCode":"00","vnp_Amount":"9900000"}', DATEADD(day, -10, SYSUTCDATETIME()));

IF NOT EXISTS (SELECT 1 FROM dbo.CreditLedger WHERE LedgerId = '9A840000-0000-0000-0000-000000000001')
INSERT dbo.CreditLedger (LedgerId, UserId, EntryType, CreditChange, BalanceAfter, ReferenceId, Description, CreatedAt)
VALUES ('9A840000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', N'Purchase', 22, 22, '9A820000-0000-0000-0000-000000000001', N'Purchased Starter Interview Pack via VNPAY.', DATEADD(day, -10, SYSUTCDATETIME()));
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Payment seed because one or more required tables are missing.';
END
GO

USE [DevPrep_Notification];
GO

IF OBJECT_ID('dbo.EmailTemplates', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.EmailLogs', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.Notifications', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.EmailTemplates WHERE TemplateId = '9A900000-0000-0000-0000-000000000001')
INSERT dbo.EmailTemplates (TemplateId, Code, Subject, BodyHtml, IsActive, CreatedAt)
VALUES ('9A900000-0000-0000-0000-000000000001', N'SEED_INTERVIEW_REPORT_READY', N'Your DevPrep interview report is ready', N'<p>Hello {{FullName}}, your interview report is ready to review.</p>', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.EmailLogs WHERE EmailLogId = '9A910000-0000-0000-0000-000000000001')
INSERT dbo.EmailLogs (EmailLogId, RecipientEmail, UserId, TemplateCode, Subject, Body, Status, ErrorMessage, RetryCount, SentAt, CreatedAt)
VALUES ('9A910000-0000-0000-0000-000000000001', N'seed.an.nguyen@devprep.local', '9A000000-0000-0000-0000-000000000003', N'SEED_INTERVIEW_REPORT_READY', N'Your DevPrep interview report is ready', N'Your backend interview report has been generated.', N'Sent', NULL, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Notifications WHERE NotificationId = '9A920000-0000-0000-0000-000000000001')
INSERT dbo.Notifications (NotificationId, UserId, Title, Message, Type, IsRead, LinkUrl, CreatedAt)
VALUES ('9A920000-0000-0000-0000-000000000001', '9A000000-0000-0000-0000-000000000003', N'Interview report ready', N'Your Backend .NET practice interview report is available.', N'Report', 0, N'/Profile/InterviewHistory', SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Notification seed because one or more required tables are missing.';
END
GO

USE [DevPrep_Statistics];
GO

IF OBJECT_ID('dbo.MetricCounters', 'U') IS NOT NULL
   AND OBJECT_ID('dbo.StatisticsSnapshots', 'U') IS NOT NULL
BEGIN
IF NOT EXISTS (SELECT 1 FROM dbo.MetricCounters WHERE MetricKey = N'seed.total_interviews_completed')
INSERT dbo.MetricCounters (MetricKey, MetricValue, UpdatedAt)
VALUES (N'seed.total_interviews_completed', 1.00, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.StatisticsSnapshots WHERE SnapshotId = '9AA00000-0000-0000-0000-000000000001')
INSERT dbo.StatisticsSnapshots (SnapshotId, SnapshotDate, NewUsers, ActiveUsers, InterviewSessions, CompletedSessions, CvUploaded, Revenue, PaidOrders, NewFeedbacks, AvgSessionScore, CreatedAt)
VALUES ('9AA00000-0000-0000-0000-000000000001', CONVERT(date, SYSUTCDATETIME()), 2, 2, 1, 1, 1, 99000.00, 1, 2, 8.20, SYSUTCDATETIME());
END
ELSE
BEGIN
    PRINT N'Skipped DevPrep_Statistics seed because one or more required tables are missing.';
END
GO

PRINT N'DevPrep sample data seed completed.';
GO
