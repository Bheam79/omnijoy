using Microsoft.EntityFrameworkCore;

namespace Omnijoy.Infrastructure.Data;

/// <summary>
/// Entity Framework Core DbContext for Omnijoy.
/// Entities (tables) will be added in OMNIJOY-3 (database schema task).
/// </summary>
public class OmnijoyDbContext : DbContext
{
    public OmnijoyDbContext(DbContextOptions<OmnijoyDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity configurations will be applied here in OMNIJOY-3.
        // modelBuilder.ApplyConfigurationsFromAssembly(typeof(OmnijoyDbContext).Assembly);
    }
}
