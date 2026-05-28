using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Omnijoy.Infrastructure.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef migrations ...).
/// Uses a hardcoded MariaDB 10.x server version so no live DB connection is
/// needed to generate or apply migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OmnijoyDbContext>
{
    public OmnijoyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OmnijoyDbContext>();

        // Use the dev connection string (override with OMNIJOY_CONN env var if needed)
        var connectionString = Environment.GetEnvironmentVariable("OMNIJOY_CONN")
            ?? "Server=localhost;Port=3306;Database=omnijoy;User=omnijoy;Password=omnijoy_pass;";

        // Hardcode MariaDB 10.11 — avoids requiring a live DB for design-time ops
        var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0));

        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new OmnijoyDbContext(optionsBuilder.Options);
    }
}
