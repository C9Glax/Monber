namespace Services.Prices.Database;

/// <summary>
/// Records that a (store, product) pair was checked against the chain's own site, independent of
/// whether a price was actually found - see PriceLookup. Kept separate from DbPriceObservation
/// (which is a pure history of actually-observed prices, returned as-is by GetPriceHistoryEndpoint)
/// so that a product a store simply doesn't stock still gets a same-day cache hit instead of being
/// re-fetched live on every request.
/// </summary>
internal record DbPriceCheck(long StoreId, string Product, DateTimeOffset LastCheckedAt);
