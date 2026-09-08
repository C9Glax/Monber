namespace Services.Prices;

/// <summary>
/// The calendar day a price belongs to, in the store's own local time. A fetched price is only valid
/// for the rest of that store-local day: chains publish prices per calendar day (and roll them over at
/// local midnight, not at 00:00 UTC), so anything fetched on an earlier day is expired regardless of
/// how many hours ago it was - see PriceLookup.
/// </summary>
internal static class StoreClock
{
    /// <summary>
    /// Every store this service prices is German (see Services.POI's brand list), so one zone covers
    /// them all. If the shared store table ever grows non-German locations, this becomes a per-store
    /// lookup off <see cref="PricedStore"/>'s coordinates - which is why callers pass the store in.
    /// </summary>
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Berlin", out TimeZoneInfo? zone)
            ? zone
            // No tz database in the container image - fall back to UTC rather than failing every
            // lookup. Day boundaries are then off by the German UTC offset (1-2h), not broken.
            : TimeZoneInfo.Utc;

    /// <summary>The store-local calendar day <paramref name="instant"/> falls on.</summary>
    internal static DateOnly LocalDate(PricedStore store, DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>The store-local calendar day it is right now.</summary>
    internal static DateOnly Today(PricedStore store) => LocalDate(store, DateTimeOffset.UtcNow);

    /// <summary>
    /// The instant a price fetched at <paramref name="fetchedAt"/> stops being valid: the start of the
    /// next store-local day. Exposed on the API so clients know when to re-request rather than guessing
    /// a fixed max age.
    /// </summary>
    internal static DateTimeOffset ValidUntil(PricedStore store, DateTimeOffset fetchedAt)
    {
        DateTime nextLocalMidnight = LocalDate(store, fetchedAt).AddDays(1).ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(nextLocalMidnight, Zone.GetUtcOffset(nextLocalMidnight));
    }
}
