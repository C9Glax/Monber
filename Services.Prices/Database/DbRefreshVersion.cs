namespace Services.Prices.Database;

internal record DbRefreshVersion(string Brand, DateTimeOffset LastRefreshedAt);
