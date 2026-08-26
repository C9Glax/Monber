import type { PoiStore, PriceObservation } from './useMonberApi'
import { fetchPoiStores, fetchPrices } from './useMonberApi'

export interface VariantDef {
  key: 'orig' | 'ultra' | 'mango'
  name: string
  product: string
  color: string
  dim: string
}

/* Loose, low-chroma nods to each can's own palette — ported from the mockup, no brand marks. */
export const VARIANTS: VariantDef[] = [
  { key: 'orig', name: 'Original', product: 'Monster Energy Original', color: 'oklch(0.74 0.125 152)', dim: 'oklch(0.40 0.075 152)' },
  { key: 'ultra', name: 'Ultra', product: 'Monster Energy Ultra', color: 'oklch(0.83 0.045 232)', dim: 'oklch(0.44 0.035 232)' },
  { key: 'mango', name: 'Mango Loco', product: 'Monster Energy Mango Loco', color: 'oklch(0.79 0.125 66)', dim: 'oklch(0.44 0.085 66)' },
]

export type VariantPrices = Record<VariantDef['key'], number | null>

export interface MergedStore {
  id: number
  brand: string
  name: string | null
  openingHours: string | null
  lat: number
  lon: number
  dist: number
  prices: VariantPrices
  latestFetchedAt: string | null
}

export interface RangedStore extends MergedStore {
  low: number
  lowVariant: VariantDef
}

export const MAX_RADIUS_KM = 30

export function haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371
  const d = Math.PI / 180
  const a =
    Math.sin(((lat2 - lat1) * d) / 2) ** 2 +
    Math.cos(lat1 * d) * Math.cos(lat2 * d) * Math.sin(((lon2 - lon1) * d) / 2) ** 2
  return 2 * R * Math.asin(Math.sqrt(a))
}

export function eur(v: number | null | undefined): string {
  return v == null ? '—' : `€${v.toFixed(2)}`
}

export function ago(iso: string | null): string {
  if (!iso) return ''
  const mins = Math.max(0, (Date.now() - new Date(iso).getTime()) / 60000)
  if (mins < 1) return 'just now'
  if (mins < 60) return `${Math.round(mins)} min ago`
  const hours = mins / 60
  if (hours < 24) return `${Math.round(hours)} h ago`
  return `${Math.round(hours / 24)} d ago`
}

function mergeStores(pois: PoiStore[], observations: PriceObservation[], userLat: number, userLon: number): MergedStore[] {
  const byStore = new Map<number, PriceObservation[]>()
  for (const obs of observations) {
    const list = byStore.get(obs.storeId)
    if (list) list.push(obs)
    else byStore.set(obs.storeId, [obs])
  }

  const merged: MergedStore[] = []
  for (const store of pois) {
    // Includes stores with no price observations at all (no StoreExternalIds mapping yet, or
    // never fetched) - they still get a MergedStore, just with every price null, so the map can
    // show them as plain "no price data" markers rather than only ever showing priced stores.
    const obs = byStore.get(store.id) ?? []

    const prices: VariantPrices = { orig: null, ultra: null, mango: null }
    let latestFetchedAt: string | null = null
    for (const variant of VARIANTS) {
      const match = obs.find((o) => o.product === variant.product)
      if (match) {
        prices[variant.key] = match.price
        if (!latestFetchedAt || new Date(match.fetchedAt) > new Date(latestFetchedAt)) {
          latestFetchedAt = match.fetchedAt
        }
      }
    }

    merged.push({
      id: store.id,
      brand: store.brand,
      name: store.name,
      openingHours: store.openingHours,
      lat: store.latitude,
      lon: store.longitude,
      dist: haversineKm(userLat, userLon, store.latitude, store.longitude),
      prices,
      latestFetchedAt,
    })
  }

  return merged
}

export function useStorePrices() {
  const pois = ref<PoiStore[]>([])
  const observations = ref<PriceObservation[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const lastQuery = ref<{ lat: number, lon: number } | null>(null)

  async function refresh(lat: number, lon: number) {
    loading.value = true
    error.value = null
    lastQuery.value = { lat, lon }
    try {
      const [poiResult, priceResult] = await Promise.all([
        fetchPoiStores(lat, lon),
        fetchPrices(lat, lon),
      ])
      pois.value = poiResult
      observations.value = priceResult
    }
    catch {
      error.value = 'Could not reach the POI/Prices services.'
    }
    finally {
      loading.value = false
    }
  }

  const merged = computed(() => {
    if (!lastQuery.value) return []
    return mergeStores(pois.value, observations.value, lastQuery.value.lat, lastQuery.value.lon)
  })

  /** Stores within `radiusKm`, cheapest price for `variantKey` (or overall) present, sorted lowest-first. */
  function inRange(radiusKm: number, variantKey: VariantDef['key'] | null = null): RangedStore[] {
    const out: RangedStore[] = []
    for (const store of merged.value) {
      if (store.dist > radiusKm) continue

      const candidates = variantKey ? [variantKey] : VARIANTS.map((v) => v.key)
      let low: number | null = null
      let lowVariant: VariantDef | null = null
      for (const key of candidates) {
        const price = store.prices[key]
        if (price != null && (low == null || price < low)) {
          low = price
          lowVariant = VARIANTS.find((v) => v.key === key)!
        }
      }
      if (low == null || !lowVariant) continue

      out.push({ ...store, low, lowVariant })
    }
    return out.sort((a, b) => a.low - b.low || a.dist - b.dist)
  }

  /** Stores within `radiusKm` that have no price data for any variant. */
  function unpricedInRange(radiusKm: number): MergedStore[] {
    return merged.value.filter((store) => store.dist <= radiusKm && VARIANTS.every((v) => store.prices[v.key] == null))
  }

  return { pois, observations, loading, error, merged, refresh, inRange, unpricedInRange }
}
