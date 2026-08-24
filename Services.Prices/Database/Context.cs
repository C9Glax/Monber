using Microsoft.EntityFrameworkCore;

namespace Services.Prices.Database;

internal class Context(DbContextOptions<Context> options) : DbContext(options)
{
    internal DbSet<DbStore> Stores { get; init; }
    internal DbSet<DbPriceObservation> Prices { get; init; }
    internal DbSet<DbRefreshVersion> Version { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbStore>()
            .ToTable("stores")
            .HasKey(s => s.Id);
        modelBuilder.Entity<DbStore>()
            .HasIndex(s => new { s.Brand, s.ExternalStoreId })
            .IsUnique();

        modelBuilder.Entity<DbPriceObservation>()
            .ToTable("price_observations")
            .HasKey(p => p.Id);
        modelBuilder.Entity<DbPriceObservation>()
            .HasIndex(p => new { p.StoreId, p.Product, p.FetchedAt });

        modelBuilder.Entity<DbRefreshVersion>()
            .ToTable("versions")
            .HasKey(v => v.Brand);
    }
}
