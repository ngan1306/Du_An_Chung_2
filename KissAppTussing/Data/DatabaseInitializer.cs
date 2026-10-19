using System.Data.Common;
using KissAppTussing.Models;
using Microsoft.EntityFrameworkCore;

namespace KissAppTussing.Data;

internal static class DatabaseInitializer
{
    private const string InitialMigrationId = "20261005083248_InitialCreate";

    private static readonly string[] InitialUserColumns =
    [
        "Id", "Name", "Email", "NormalizedEmail", "PasswordHash", "BirthDate", "Bio", "CreatedAt"
    ];

    private static readonly string[] DefaultInterests =
    [
        "Âm nhạc", "Du lịch", "Đọc sách", "Nấu ăn", "Phim ảnh", "Thể thao", "Cà phê",
        "Nhiếp ảnh", "Thiên nhiên", "Công nghệ", "Nghệ thuật", "Yoga", "Thú cưng", "Game"
    ];

    public static async Task InitializeAsync(DatingDbContext database, CancellationToken cancellationToken = default)
    {
        await database.Database.OpenConnectionAsync(cancellationToken);
        var appliedMigrations = await database.Database.GetAppliedMigrationsAsync(cancellationToken);
        var userColumns = await GetUserColumnsAsync(database.Database.GetDbConnection(), cancellationToken);

        if (!appliedMigrations.Any() && userColumns.Count > 0)
        {
            if (InitialUserColumns.Any(column => !userColumns.Contains(column)))
            {
                throw new InvalidOperationException(
                    "The existing Users table does not match the InitialCreate schema; database migration was stopped to protect data.");
            }

            await RecordInitialMigrationAsync(database.Database.GetDbConnection(), cancellationToken);
        }

        await database.Database.MigrateAsync(cancellationToken);

        if (!await database.Interests.AnyAsync(cancellationToken))
        {
            database.Interests.AddRange(DefaultInterests.Select(name => new Interest { Name = name }));
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<HashSet<string>> GetUserColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Users\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task RecordInitialMigrationAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261005083248_InitialCreate', '10.0.12');
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}