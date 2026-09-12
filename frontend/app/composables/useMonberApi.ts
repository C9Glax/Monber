import type { components as PoiComponents } from '~/types/api/poi'
import type { components as PricesComponents } from '~/types/api/prices'
import { usePoiApi } from './useApiFetcher'

export interface PoiStore {
  id: number
  name: string | null
  latitude: number
  longitude: number
  brand: string
  openingHours: string | null
}

export interface PriceObservation {
  storeId: number
  brand: string
  storeName: string | null
  product: string
  price: number
  currency: string
  fetchedAt: string
  /** Set when this price isn't active yet - it becomes effective on this date (e.g. an upcoming sale). */
  effectiveFrom: string | null
  /** The page this price was fetched from, if known. */
  sourceUrl: string | null
}

/** One line of the `/prices` NDJSON stream - reports one store's resolved prices as soon as they're known. */
export interface PriceStreamEvent {
  storeId: number
  /** False if the store was checked but has no tracked prices - distinct from not having been checked yet. */
  hasPrices: boolean
  observations: PriceObservation[]
}

// The backend's OpenAPI document (see ~/types/api/*) reports int64/double fields as `number |
// string`, since a JSON number can't safely carry every int64 value - .NET's OpenAPI generator
// documents both encodings as valid. Every value it actually sends is a JSON number, so this app
// normalizes straight back to `number` at the fetch boundary rather than threading that union
// through every call site.
function n(value: number | string): number {
  return typeof value === 'string' ? Number(value) : value
}

function toPoiStore(dto: PoiComponents['schemas']['Store']): PoiStore {
  return {
    id: n(dto.id),
    name: dto.name,
    latitude: n(dto.latitude),
    longitude: n(dto.longitude),
    brand: dto.brand,
    openingHours: dto.openingHours,
  }
}

function toPriceObservation(dto: PricesComponents['schemas']['PriceObservation']): PriceObservation {
  return {
    storeId: n(dto.storeId),
    brand: dto.brand,
    storeName: dto.storeName,
    product: dto.product,
    price: n(dto.price),
    currency: dto.currency,
    fetchedAt: dto.fetchedAt,
    effectiveFrom: dto.effectiveFrom ?? null,
    sourceUrl: dto.sourceUrl ?? null,
  }
}

function apiBase(): string {
  return useRuntimeConfig().public.apiBase
}

export function poiStoresUrl(lat: number, lon: number): string {
  return `${apiBase()}/poi/stores?lat=${lat}&lon=${lon}`
}

export function pricesUrl(lat: number, lon: number): string {
  return `${apiBase()}/prices/prices?lat=${lat}&lon=${lon}`
}

export function refreshStorePricesUrl(storeId: number): string {
  return `${apiBase()}/prices/prices/store/refresh?storeId=${storeId}`
}

export async function fetchPoiStores(lat: number, lon: number, signal?: AbortSignal): Promise<PoiStore[]> {
  const { data, error } = await usePoiApi().GET('/stores', { params: { query: { lat, lon } }, signal })
  if (error) throw new Error('POI stores request failed')
  return data.map(toPoiStore)
}

/** Forces a live re-fetch of a single store's prices, bypassing the once-per-day cache. */
export async function refreshStorePrices(storeId: number, signal?: AbortSignal): Promise<PriceObservation[]> {
  return await $fetch<PriceObservation[]>(refreshStorePricesUrl(storeId), { method: 'POST', signal })
}

/**
 * Reads the `/prices` NDJSON stream and invokes `onEvent` for each line as it arrives, so callers
 * can render prices progressively instead of waiting for the whole (potentially slow) response.
 * Uses raw `fetch` rather than the typed `usePricesApi()` client, which (like `$fetch`/ofetch)
 * buffers the entire response body before resolving and would defeat the purpose of streaming -
 * the generated `PriceStreamEvent` schema (see ~/types/api/prices, and the `Produces<>` annotation
 * on `GET /prices` that puts it there) still types each parsed line.
 */
export async function streamPrices(
  lat: number, lon: number, onEvent: (event: PriceStreamEvent) => void, signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(pricesUrl(lat, lon), { signal })
  if (!response.ok || !response.body) throw new Error(`Prices stream failed: ${response.status}`)

  function parseEvent(line: string): PriceStreamEvent {
    const raw = JSON.parse(line) as PricesComponents['schemas']['PriceStreamEvent']
    return {
      storeId: n(raw.storeId),
      hasPrices: raw.hasPrices,
      observations: raw.observations.map(toPriceObservation),
    }
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    let newlineIndex: number
    while ((newlineIndex = buffer.indexOf('\n')) !== -1) {
      const line = buffer.slice(0, newlineIndex)
      buffer = buffer.slice(newlineIndex + 1)
      if (line.trim().length > 0) onEvent(parseEvent(line))
    }
  }
  if (buffer.trim().length > 0) onEvent(parseEvent(buffer))
}
