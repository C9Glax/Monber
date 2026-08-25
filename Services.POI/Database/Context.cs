using Microsoft.EntityFrameworkCore;

namespace Services.POI.Database;

internal class Context(DbContextOptions<Context> options) : DbContext(options)
{
    internal DbSet<DbStore> Stores { get; init; }
    internal DbSet<DbVersion> Version { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbStore>()
            .ToTable("stores")
            .HasKey(s => s.Id);

        modelBuilder.Entity<DbVersion>()
            .ToTable("versions")
            .HasKey(v => v.Id);
    }
}