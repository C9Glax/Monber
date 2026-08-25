namespace Services.Prices.Database;

/// <summary>
/// Maps a chain's own store identifier to the matching row in the shared `stores` table (owned by
/// Services.POI - see Context.cs). The natural key is (Brand, ExternalStoreId), same as a chain
/// fetcher discovers it; StoreId is a plain column, not part of the key, so re-pointing a mapping
/// at a different matched store is a normal update, never a primary-key change.
/// </summary>
public record DbStoreExternalId(string Brand, string ExternalStoreId, long StoreId);
