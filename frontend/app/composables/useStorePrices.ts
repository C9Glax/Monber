import type { PoiStore, PriceObservation } from './useMonberApi'
import { fetchPoiStores, streamPrices } from './useMonberApi'

/** A tracked pack size. Stores price every flavor of a given pack identically, so flavor isn't tracked -
 * only pack size, since that's what actually moves the per-can price (bulk packs are cheaper per can). */
export interface PackDef {
  key: 'single' | 'four' | 'ten'
  label: string
  product: string
  cans: number
}

export const PACKS: PackDef[] = [
  { key: 'single', label: 'Single can', product: 'Monster Energy 0,5l', cans: 1 },
  { key: 'four', label: '4-pack', product: 'Monster Energy 4x0,5l', cans: 4 },
  { key: 'ten', label: '10-pack', product: 'Monster Energy 10x0,5l', cans: 10 },
]

export interface MergedStore {
  id: number
  brand: string
  name: string | null
  openingHours: string | null
  lat: number
  lon: number
  dist: number
  /** Cheapest observed price per single can, normalized across pack sizes (pack price / can count). */
  perCanPrice: number | null
  /** Which pack size produced `perCanPrice`, for display only - not a selectable filter. */
  pack: string | null
  latestFetchedAt: string | null
  /** Set when `perCanPrice` comes from a future/upcoming price (see PriceObservation.effectiveFrom). */
  effectiveFrom: string | null
  /** The page `perCanPrice` was fetched from, if known. */
  sourceUrl: string | null
  /** Whether this store's prices have been checked yet - 'pending' until the price stream reports on it. */
  priceStatus: 'pending' | 'priced' | 'empty'
}

export interface RangedStore extends MergedStore {
  low: number
}

export const MAX_RADIUS_KM = 10

export function haversineKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371
  const d = Math.PI / 180
  const a =
    Math.sin(((lat2 - lat1) * d) / 2) ** 2 +
    Math.cos(lat1 * d) * Math.cos(lat2 * d) * Math.sin(((lon2 - lon1) * d) / 2) ** 2
  return 2 * R * Math.asin(Math.sqrt(a))
}

export function eur(v: number | null | undefined): string {
  return v == null ? '€-.--' : `€${v.toFixed(2)}`
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

/**
 * @param wantFuture When false (default), only considers current-price observations
 * (`effectiveFrom == null`) - the store's price as observed today. When true, only considers
 * upcoming ones (`effectiveFrom` set), e.g. a sale that hasn't started yet - see
 * PriceObservation.effectiveFrom and KauflandPriceFetcher on the backend.
 */
function mergeStores(
  pois: PoiStore[], observations: PriceObservation[], storeStatus: Map<number, 'pending' | 'priced' | 'empty'>,
  userLat: number, userLon: number, wantFuture = false,
): MergedStore[] {
  const relevant = observations.filter((o) => (o.effectiveFrom != null) === wantFuture)

  const byStore = new Map<number, PriceObservation[]>()
  for (const obs of relevant) {
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

    let perCanPrice: number | null = null
    let pack: string | null = null
    let latestFetchedAt: string | null = null
    let effectiveFrom: string | null = null
    let sourceUrl: string | null = null
    for (const packDef of PACKS) {
      const match = obs.find((o) => o.product === packDef.product)
      if (!match) continue

      const canPrice = match.price / packDef.cans
      if (perCanPrice == null || canPrice < perCanPrice) {
        perCanPrice = canPrice
        pack = packDef.label
        effectiveFrom = match.effectiveFrom
        sourceUrl = match.sourceUrl
      }
      if (!latestFetchedAt || new Date(match.fetchedAt) > new Date(latestFetchedAt)) {
        latestFetchedAt = match.fetchedAt
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
      perCanPrice,
      pack,
      latestFetchedAt,
      effectiveFrom,
      sourceUrl,
      priceStatus: storeStatus.get(store.id) ?? 'pending',
    })
  }

  return merged
}

export function useStorePrices() {
  const pois = ref<PoiStore[]>([])
  const observations = ref<PriceObservation[]>([])
  const storeStatus = ref<Map<number, 'pending' | 'priced' | 'empty'>>(new Map())
  const loading = ref(false)
  /** True while the price stream for the current query is still delivering stores. */
  const pricesLoading = ref(false)
  const error = ref<string | null>(null)
  const lastQuery = ref<{ lat: number, lon: number } | null>(null)

  // Not reactive state - just tracks which call is the latest, so a superseded location change
  // can abort its own request and be ignored if it somehow still resolves.
  let activeController: AbortController | null = null

  async function refresh(lat: number, lon: number) {
    activeController?.abort()
    const controller = new AbortController()
    activeController = controller

    loading.value = true
    error.value = null
    observations.value = []
    lastQuery.value = { lat, lon }
    try {
      const poiResult = await fetchPoiStores(lat, lon, controller.signal)
      if (controller.signal.aborted) return
      pois.value = poiResult
      storeStatus.value = new Map(poiResult.map((store) => [store.id, 'pending' as const]))
    }
    catch (err) {
      if (controller.signal.aborted) return
      console.error('POI fetch failed:', err)
      error.value = 'Could not reach the POI service.'
      loading.value = false
      return
    }
    loading.value = false

    pricesLoading.value = true
    try {
      await streamPrices(lat, lon, (event) => {
        if (controller.signal.aborted) return
        if (event.observations.length > 0) observations.value = [...observations.value, ...event.observations]
        storeStatus.value = new Map(storeStatus.value).set(event.storeId, event.hasPrices ? 'priced' : 'empty')
      }, controller.signal)
    }
    catch (err) {
      if (controller.signal.aborted) return
      console.error('Prices stream failed:', err)
      error.value = 'Could not reach the Prices service.'
    }
    finally {
      if (activeController === controller) pricesLoading.value = false
    }
  }

  const merged = computed(() => {
    if (!lastQuery.value) return []
    return mergeStores(pois.value, observations.value, storeStatus.value, lastQuery.value.lat, lastQuery.value.lon)
  })

  /** Same as `merged`, but built only from upcoming/future prices (see PriceObservation.effectiveFrom). */
  const mergedFuture = computed(() => {
    if (!lastQuery.value) return []
    return mergeStores(pois.value, observations.value, storeStatus.value, lastQuery.value.lat, lastQuery.value.lon, true)
  })

  function rangeFrom(source: MergedStore[], radiusKm: number): RangedStore[] {
    const out: RangedStore[] = []
    for (const store of source) {
      if (store.dist > radiusKm || store.perCanPrice == null) continue
      out.push({ ...store, low: store.perCanPrice })
    }
    return out.sort((a, b) => a.low - b.low || a.dist - b.dist)
  }

  /** Stores within `radiusKm` with a per-can price, sorted lowest-first. */
  function inRange(radiusKm: number): RangedStore[] {
    return rangeFrom(merged.value, radiusKm)
  }

  /** Same as `inRange`, but for upcoming/future prices only. */
  function inRangeFuture(radiusKm: number): RangedStore[] {
    return rangeFrom(mergedFuture.value, radiusKm)
  }

  /** Stores within `radiusKm` not yet checked by the price stream. */
  function pendingInRange(radiusKm: number): MergedStore[] {
    return merged.value.filter((store) => store.dist <= radiusKm && store.priceStatus === 'pending')
  }

  /** Stores within `radiusKm` that were checked and confirmed to have no price data. */
  function emptyInRange(radiusKm: number): MergedStore[] {
    return merged.value.filter((store) => store.dist <= radiusKm && store.priceStatus === 'empty')
  }

  return {
    pois, observations, loading, pricesLoading, error, merged, mergedFuture,
    refresh, inRange, inRangeFuture, pendingInRange, emptyInRange,
  }
}
