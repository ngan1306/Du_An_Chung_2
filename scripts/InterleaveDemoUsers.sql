USE [KissAppTussing];
GO

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @NowUnixMs bigint = DATEDIFF_BIG(
    MILLISECOND,
    CONVERT(datetime2(3), '1970-01-01T00:00:00'),
    SYSUTCDATETIME());
DECLARE @ExistingUserCount int =
(
    SELECT COUNT(*)
    FROM dbo.Users
    WHERE NormalizedEmail NOT LIKE N'SEED.USER.%@KISS.LOCAL'
);
DECLARE @LatestExistingCreatedAt bigint =
(
    SELECT MAX(CreatedAt)
    FROM dbo.Users
    WHERE NormalizedEmail NOT LIKE N'SEED.USER.%@KISS.LOCAL'
);
DECLARE @UpdatedUsers int = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH ExistingUsers AS
    (
        SELECT
            Id,
            CreatedAt,
            ROW_NUMBER() OVER (ORDER BY CreatedAt, Id) AS UserNumber
        FROM dbo.Users
        WHERE NormalizedEmail NOT LIKE N'SEED.USER.%@KISS.LOCAL'
    ),
    SeedUsers AS
    (
        SELECT
            Id,
            ROW_NUMBER() OVER (ORDER BY NormalizedEmail, Id) AS SeedNumber
        FROM dbo.Users
        WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
    )
    UPDATE SeedUser
    SET CreatedAt = CASE
        WHEN SeedUsers.SeedNumber <= @ExistingUserCount
            THEN ExistingUsers.CreatedAt + 1
        ELSE COALESCE(@LatestExistingCreatedAt, @NowUnixMs)
            + (SeedUsers.SeedNumber - @ExistingUserCount)
    END
    FROM dbo.Users AS SeedUser
    INNER JOIN SeedUsers ON SeedUsers.Id = SeedUser.Id
    LEFT JOIN ExistingUsers ON ExistingUsers.UserNumber = SeedUsers.SeedNumber;

    SET @UpdatedUsers = @@ROWCOUNT;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    @ExistingUserCount AS ExistingUsers,
    @UpdatedUsers AS InterleavedDemoUsers,
    (SELECT COUNT(*) FROM dbo.Users) AS TotalUsers;
