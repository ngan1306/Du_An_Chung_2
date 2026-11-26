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

DECLARE @NowUtc datetime2(3) = SYSUTCDATETIME();
DECLARE @NowUnixMs bigint = DATEDIFF_BIG(MILLISECOND, CONVERT(datetime2(3), '1970-01-01T00:00:00'), @NowUtc);
DECLARE @LatestExistingUserCreatedAt bigint =
(
    SELECT MAX(CreatedAt)
    FROM dbo.Users
    WHERE NormalizedEmail NOT LIKE N'SEED.USER.%@KISS.LOCAL'
);
DECLARE @UpdatedSeedUsers int = 0;
DECLARE @AddedPhotos int = 0;
DECLARE @UpdatedPhotos int = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH SeedUsers AS
    (
        SELECT
            Id,
            ROW_NUMBER() OVER (ORDER BY NormalizedEmail) AS SeedNumber
        FROM dbo.Users
        WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
    )
    UPDATE UserRow
    SET CreatedAt = COALESCE(@LatestExistingUserCreatedAt, @NowUnixMs) + SeedUsers.SeedNumber
    FROM dbo.Users AS UserRow
    INNER JOIN SeedUsers ON SeedUsers.Id = UserRow.Id;

    SET @UpdatedSeedUsers = @@ROWCOUNT;

    ;WITH SeedUsers AS
    (
        SELECT
            Id,
            TRY_CONVERT(int, SUBSTRING(NormalizedEmail, 11, 3)) AS SeedNumber
        FROM dbo.Users
        WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
    )
    UPDATE Photo
    SET Url = CONCAT(
        N'/demo-profiles/seed-',
        RIGHT(CONCAT(N'000', SeedUsers.SeedNumber), 3),
        N'.svg')
    FROM dbo.ProfilePhotos AS Photo
    INNER JOIN SeedUsers ON SeedUsers.Id = Photo.UserId
    WHERE Photo.IsAvatar = 1;

    SET @UpdatedPhotos = @@ROWCOUNT;

    ;WITH SeedUsers AS
    (
        SELECT
            Id,
            ROW_NUMBER() OVER (ORDER BY NormalizedEmail) AS SeedNumber
        FROM dbo.Users
        WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
    )
    INSERT INTO dbo.ProfilePhotos (Id, UserId, Url, IsAvatar, SortOrder, CreatedAt)
    SELECT
        NEWID(),
        SeedUsers.Id,
            CONCAT(N'/demo-profiles/seed-', RIGHT(CONCAT(N'000', SeedUsers.SeedNumber), 3), N'.svg'),
        1,
        0,
        @NowUnixMs
    FROM SeedUsers
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.ProfilePhotos AS ExistingPhoto
        WHERE ExistingPhoto.UserId = SeedUsers.Id
          AND ExistingPhoto.IsAvatar = 1
    );

    SET @AddedPhotos = @@ROWCOUNT;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    @UpdatedSeedUsers AS SeedUsersMovedAfterExistingUsers,
    @UpdatedPhotos AS SeedAvatarsUpdated,
    @AddedPhotos AS DemoPhotosAdded,
    (SELECT COUNT(*) FROM dbo.Users) AS TotalUsers,
    (
        SELECT COUNT(*)
        FROM dbo.Users AS SeedUser
        WHERE SeedUser.NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
          AND EXISTS
          (
              SELECT 1
              FROM dbo.ProfilePhotos AS Photo
              WHERE Photo.UserId = SeedUser.Id
                AND Photo.IsAvatar = 1
          )
    ) AS SeedUsersWithAvatar;
