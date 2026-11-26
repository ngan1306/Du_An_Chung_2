USE [KissAppTussing];
GO

DECLARE @AdminUserId UNIQUEIDENTIFIER =
    'B5790F0D-E629-434A-A3CD-9DBAB8CBB438';

SELECT
    Id,
    Name,
    Email,
    BirthDate,
    Gender,
    City,
    CASE
        WHEN Id = @AdminUserId THEN N'Quản trị viên'
        ELSE N'Người dùng'
    END AS VaiTro
FROM dbo.Users
ORDER BY
    CASE WHEN Id = @AdminUserId THEN 0 ELSE 1 END,
    Name;