using Microsoft.EntityFrameworkCore;
using MonberAPI.PoiData.Database;

namespace Services.Prices.Database;

internal class Context(DbContextOptions<Context> options) : DbContext(options)
{
    /// <summary>The shared `stores` table - owned/migrated by Services.POI only; see OnModelCreating.</summary>
    internal DbSet<DbStore> Stores { get; init; }

    internal DbSet<DbStoreExternalId> StoreExternalIds { get; init; }
    internal DbSet<DbPriceObservation> Prices { get; init; }
    internal DbSet<DbRefreshVersion> Version { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Read/join only - Services.POI owns this table's schema and is the only writer.
        modelBuilder.Entity<DbStore>()
            .ToTable("stores", t => t.ExcludeFromMigrations())
            .HasKey(s => s.Id);

        modelBuilder.Entity<DbStoreExternalId>()
            .ToTable("store_external_ids")
            .HasKey(e => new { e.Brand, e.ExternalStoreId });
        modelBuilder.Entity<DbStoreExternalId>()
            .HasIndex(e => e.StoreId)
            .IsUnique();

        modelBuilder.Entity<DbPriceObservation>()
            .ToTable("price_observations")
            .HasKey(p => p.Id);
        modelBuilder.Entity<DbPriceObservation>()
            .HasIndex(p => new { p.StoreId, p.Product, p.FetchedAt });

        // Named "price_sync_versions", not "versions" - Services.POI's own `versions` table lives in
        // this same physical database now.
        modelBuilder.Entity<DbRefreshVersion>()
            .ToTable("price_sync_versions")
            .HasKey(v => v.Brand);
    }
}
