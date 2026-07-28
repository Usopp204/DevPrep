-- Idempotent schema upgrade for normalized question positions.
-- Run this before importing question data that references PositionId.
USE [DevPrep_Question];
GO

SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Questions', N'U') IS NULL
    THROW 50001, 'dbo.Questions does not exist. Run the base Question database script first.', 1;
GO

IF OBJECT_ID(N'dbo.Positions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Positions
    (
        PositionId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_Positions_PositionId DEFAULT (newsequentialid()),
        Name NVARCHAR(50) NOT NULL,
        IsDeleted BIT NOT NULL
            CONSTRAINT DF_Positions_IsDeleted DEFAULT ((0)),
        DeletedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Positions_CreatedAt DEFAULT (dateadd(hour, (7), sysutcdatetime())),
        UpdatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_Positions_UpdatedAt DEFAULT (dateadd(hour, (7), sysutcdatetime())),
        CONSTRAINT PK_Positions PRIMARY KEY (PositionId)
    );
END
GO

IF COL_LENGTH(N'dbo.Positions', N'Description') IS NOT NULL
BEGIN
    DECLARE @descriptionDefault sysname;
    SELECT @descriptionDefault = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Positions')
      AND c.name = N'Description';

    IF @descriptionDefault IS NOT NULL
    BEGIN
        DECLARE @dropDescriptionDefault NVARCHAR(MAX) =
            N'ALTER TABLE dbo.Positions DROP CONSTRAINT ' + QUOTENAME(@descriptionDefault);
        EXEC sys.sp_executesql @dropDescriptionDefault;
    END

    ALTER TABLE dbo.Positions DROP COLUMN Description;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Positions')
      AND name = N'UQ_Positions_Name'
)
    CREATE UNIQUE INDEX UQ_Positions_Name
        ON dbo.Positions (Name)
        WHERE IsDeleted = 0;
GO

INSERT INTO dbo.Positions (Name)
SELECT seed.Name
FROM (VALUES
    (N'Backend'),
    (N'Frontend'),
    (N'Database'),
    (N'Language'),
    (N'Problem Solving'),
    (N'System Design')
) seed(Name)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Positions position
    WHERE position.Name = seed.Name
      AND position.IsDeleted = 0
);
GO

IF COL_LENGTH(N'dbo.Questions', N'PositionId') IS NULL
    ALTER TABLE dbo.Questions ADD PositionId UNIQUEIDENTIFIER NULL;
GO

-- Preserve every legacy text position before removing the old column.
IF COL_LENGTH(N'dbo.Questions', N'Position') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        INSERT INTO dbo.Positions (Name)
        SELECT DISTINCT LTRIM(RTRIM(question.Position))
        FROM dbo.Questions question
        WHERE question.Position IS NOT NULL
          AND LTRIM(RTRIM(question.Position)) <> N''''
          AND NOT EXISTS (
              SELECT 1
              FROM dbo.Positions position
              WHERE position.Name = LTRIM(RTRIM(question.Position))
                AND position.IsDeleted = 0
          );

        UPDATE question
        SET PositionId = position.PositionId
        FROM dbo.Questions question
        JOIN dbo.Positions position
          ON position.Name = LTRIM(RTRIM(question.Position))
         AND position.IsDeleted = 0
        WHERE question.Position IS NOT NULL
          AND question.PositionId IS NULL;';

    DECLARE @positionDefault sysname;
    SELECT @positionDefault = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c
      ON c.object_id = dc.parent_object_id
     AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Questions')
      AND c.name = N'Position';

    IF @positionDefault IS NOT NULL
    BEGIN
        DECLARE @dropPositionDefault NVARCHAR(MAX) =
            N'ALTER TABLE dbo.Questions DROP CONSTRAINT ' + QUOTENAME(@positionDefault);
        EXEC sys.sp_executesql @dropPositionDefault;
    END

    ALTER TABLE dbo.Questions DROP COLUMN Position;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.Questions')
      AND name = N'FK_Q_Position'
)
    ALTER TABLE dbo.Questions WITH CHECK
        ADD CONSTRAINT FK_Q_Position
        FOREIGN KEY (PositionId) REFERENCES dbo.Positions (PositionId);
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Questions')
      AND name = N'IX_Q_Position'
)
    CREATE INDEX IX_Q_Position
        ON dbo.Questions (PositionId, IsDeleted);
GO

UPDATE STATISTICS dbo.Questions;
UPDATE STATISTICS dbo.Positions;
GO
