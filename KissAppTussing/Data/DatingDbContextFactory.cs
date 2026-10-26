using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KissAppTussing.Data;

internal sealed class DatingDbContextFactory : IDesignTimeDbContextFactory<DatingDbContext>
{
    public DatingDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DatingApp")
            ?? throw new InvalidOperationException("ConnectionStrings:DatingApp is not configured.");
        var options = new DbContextOptionsBuilder<DatingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DatingDbContext(options);
    }
}
