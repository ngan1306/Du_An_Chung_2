USE [KissAppTussing];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @TargetUserCount int = 120;
DECLARE @CurrentUserCount int = (SELECT COUNT(*) FROM dbo.Users);
DECLARE @UsersToAdd int = CASE
    WHEN @TargetUserCount > @CurrentUserCount THEN @TargetUserCount - @CurrentUserCount
    ELSE 0
END;
DECLARE @NowUtc datetime2(3) = SYSUTCDATETIME();
DECLARE @NowUnixMs bigint = DATEDIFF_BIG(MILLISECOND, CONVERT(datetime2(3), '1970-01-01T00:00:00'), @NowUtc);
DECLARE @TodayUtc date = CONVERT(date, @NowUtc);
DECLARE @DemoPasswordHash nvarchar(512) = N'AQAAAAIAAYagAAAAEE8NlBS4gefqiBAUijwpapjFN2gPbMKg0yFc+m9BiXjp6wLSmk+oW+AlxHCXjAzT7A==';
DECLARE @InsertedUsers TABLE (Id uniqueidentifier NOT NULL);

IF OBJECT_ID('tempdb..#DemoUserNumbers') IS NOT NULL
    DROP TABLE #DemoUserNumbers;

CREATE TABLE #DemoUserNumbers
(
    N int NOT NULL PRIMARY KEY
);

BEGIN TRY
    BEGIN TRANSACTION;

    IF @UsersToAdd > 0
    BEGIN
        ;WITH Numbers AS
        (
            SELECT 1 AS N
            UNION ALL
            SELECT N + 1
            FROM Numbers
            WHERE N < @TargetUserCount
        )
        INSERT INTO #DemoUserNumbers (N)
        SELECT TOP (@UsersToAdd) Numbers.N
        FROM Numbers
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.Users AS ExistingUser
            WHERE ExistingUser.NormalizedEmail = UPPER(
                CONCAT(N'seed.user.', RIGHT(CONCAT(N'000', Numbers.N), 3), N'@kiss.local'))
        )
        ORDER BY Numbers.N
        OPTION (MAXRECURSION 0);

        INSERT INTO dbo.Users
        (
            Id,
            Name,
            Email,
            NormalizedEmail,
            PasswordHash,
            BirthDate,
            Bio,
            Gender,
            City,
            Occupation,
            Education,
            LookingFor,
            TermsAcceptedAt,
            CreatedAt
        )
        OUTPUT inserted.Id INTO @InsertedUsers (Id)
        SELECT
            NEWID(),
            CONCAT(
                CASE ProfileNumber.NameCycle
                    WHEN 1 THEN N'Nguyễn '
                    WHEN 2 THEN N'Trần '
                    ELSE N'Lê '
                END,
                ProfileNames.GivenName),
            Email.EmailAddress,
            UPPER(Email.EmailAddress),
            @DemoPasswordHash,
            DATEADD(YEAR, -(21 + (N.N % 25)), @TodayUtc),
            CONCAT(N'Synthetic demo profile ', RIGHT(CONCAT(N'000', N.N), 3), N' for local testing.'),
            CASE N.N % 4
                WHEN 0 THEN N'Woman'
                WHEN 1 THEN N'Man'
                WHEN 2 THEN N'NonBinary'
                ELSE N'PreferNotToSay'
            END,
            CASE N.N % 6
                WHEN 0 THEN N'Hanoi'
                WHEN 1 THEN N'Ho Chi Minh City'
                WHEN 2 THEN N'Da Nang'
                WHEN 3 THEN N'Can Tho'
                WHEN 4 THEN N'Hue'
                ELSE N'Hai Phong'
            END,
            CASE N.N % 5
                WHEN 0 THEN N'Designer'
                WHEN 1 THEN N'Engineer'
                WHEN 2 THEN N'Teacher'
                WHEN 3 THEN N'Marketing'
                ELSE N'Photographer'
            END,
            N'University',
            N'Dating',
            @NowUnixMs,
            @NowUnixMs - CONVERT(bigint, N.N) * 86400000
        FROM #DemoUserNumbers AS N
        CROSS APPLY
        (
            SELECT
                ((N.N - 1) % 36) + 1 AS PhotoNumber,
                ((N.N - 1) / 36) + 1 AS NameCycle
        ) AS ProfileNumber
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
            ON ProfileNames.PhotoNumber = ProfileNumber.PhotoNumber
        CROSS APPLY
        (
            SELECT CONCAT(N'seed.user.', RIGHT(CONCAT(N'000', N.N), 3), N'@kiss.local') AS EmailAddress
        ) AS Email;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT
    (SELECT COUNT(*) FROM dbo.Users) AS TotalUsers,
    (SELECT COUNT(*) FROM @InsertedUsers) AS UsersAddedByThisRun,
    (SELECT COUNT(*) FROM dbo.Users WHERE NormalizedEmail LIKE N'SEED.USER.%@KISS.LOCAL') AS SeedDemoUsers;

DROP TABLE #DemoUserNumbers;
