repo: C9Glax/Monber
branch: main

## Last sync

date: 2026-08-25T19:45:00Z

### Updated in this project

- Built the public price page (`Monber.dc.html`): nearest-cheapest hero, per-variant lows, live map, ranked store list.
- Store list, brands and price shape mirror `Services.Prices` DTOs (`PriceObservation`, `StoreSummary`).
- Radius control mirrors the 30 km haversine query in `GetPricesByLocationEndpoint`.
- Tracked variants taken from `TrackedProducts.All`.

## Screen map

| Screen | Built from |
| --- | --- |
| Monber.dc.html | Services.Prices/Features/GetPricesByLocationEndpoint.cs, Services.Prices/Entities/PriceObservation.cs, Services.Prices/Entities/StoreSummary.cs, Services.Prices/TrackedProducts.cs, MonberAPI.Gateway/wwwroot/index.html, CLAUDE.md |
