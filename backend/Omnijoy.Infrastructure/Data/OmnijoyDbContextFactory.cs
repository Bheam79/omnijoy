using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Omnijoy.Infrastructure.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to create a <see cref="OmnijoyDbContext"/>
/// without needing a running application host or a live database connection.
/// Uses a hardcoded MariaDB 11 server version to avoid auto-detection network calls.
/// </summary>
public class OmnijoyDbContextFactory : IDesignTimeDbContextFactory<OmnijoyDbContext>
{
    public OmnijoyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OmnijoyDbContext>();

        // Use a dummy connection string + hardcoded server version so no live DB is required.
        optionsBuilder.UseMySql(
            "Server=localhost;Database=omnijoy;User=omnijoy;Password=omnijoy_pass;",
            new MariaDbServerVersion(new Version(11, 0, 0)));

        return new OmnijoyDbContext(optionsBuilder.Options);
    }
}
