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

DECLARE @UpdatedUsers int = 0;
DECLARE @UpdatedAvatars int = 0;

BEGIN TRY
    BEGIN TRANSACTION;

    ;WITH SeedUsers AS
    (
        SELECT
            Id,
            TRY_CONVERT(int, SUBSTRING(NormalizedEmail, 11, 3)) AS SeedNumber
        FROM dbo.Users
        WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
    )
    UPDATE SeedUser
    SET Name = CONCAT(
        CASE
            WHEN SeedUsers.SeedNumber <= 36 THEN N'Nguyễn '
            WHEN SeedUsers.SeedNumber <= 72 THEN N'Trần '
            ELSE N'Lê '
        END,
        ProfileNames.GivenName)
    FROM dbo.Users AS SeedUser
    INNER JOIN SeedUsers ON SeedUsers.Id = SeedUser.Id
    INNER JOIN (VALUES
        (1, N'An'), (2, N'Linh'), (3, N'Mai'), (4, N'Thảo'), (5, N'Vy'),
        (6, N'Hà'), (7, N'Ngọc'), (8, N'My'), (9, N'Trâm'), (10, N'Trang'),
        (11, N'Minh'), (12, N'Quân'), (13, N'Duy'), (14, N'Khang'), (15, N'Phúc'),
        (16, N'Hoàng'), (17, N'Bảo'), (18, N'Nam'), (19, N'Tuấn'), (20, N'Đức'),
        (21, N'Bình'), (22, N'Khánh'), (23, N'Gia'), (24, N'Trúc'), (25, N'Nhật'),
        (26, N'Thiên'), (27, N'Hải'), (28, N'Mộc'), (29, N'Nhi'), (30, N'Lam'),
        (31, N'Vân'), (32, N'Tâm'), (33, N'Yến'), (34, N'Sơn'), (35, N'Diệp'),
        (36, N'Lộc')
    ) AS ProfileNames(PhotoNumber, GivenName)
        ON ProfileNames.PhotoNumber = ((SeedUsers.SeedNumber - 1) % 36) + 1;

    SET @UpdatedUsers = @@ROWCOUNT;

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

    SET @UpdatedAvatars = @@ROWCOUNT;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    @UpdatedUsers AS NamedDemoUsers,
    @UpdatedAvatars AS UniqueDemoAvatars,
    (SELECT COUNT(*) FROM dbo.Users) AS TotalUsers,
    (SELECT COUNT(*)
     FROM dbo.Users
     WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL'
       AND Name LIKE N'Demo User %') AS RemainingGenericDemoNames;
