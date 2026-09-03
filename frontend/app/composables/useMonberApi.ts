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

function apiBase(): string {
  return useRuntimeConfig().public.apiBase
}

export function poiStoresUrl(lat: number, lon: number): string {
  return `${apiBase()}/poi/stores?lat=${lat}&lon=${lon}`
}

export function pricesUrl(lat: number, lon: number): string {
  return `${apiBase()}/prices/prices?lat=${lat}&lon=${lon}`
}

export async function fetchPoiStores(lat: number, lon: number, signal?: AbortSignal): Promise<PoiStore[]> {
  return await $fetch<PoiStore[]>(poiStoresUrl(lat, lon), { signal })
}

export async function fetchPrices(lat: number, lon: number, signal?: AbortSignal): Promise<PriceObservation[]> {
  return await $fetch<PriceObservation[]>(pricesUrl(lat, lon), { signal })
}

/**
 * Reads the `/prices` NDJSON stream and invokes `onEvent` for each line as it arrives, so callers
 * can render prices progressively instead of waiting for the whole (potentially slow) response.
 * Uses raw `fetch` rather than `$fetch`/ofetch, which buffers the entire response body before
 * resolving and would defeat the purpose of streaming.
 */
export async function streamPrices(
  lat: number, lon: number, onEvent: (event: PriceStreamEvent) => void, signal?: AbortSignal,
): Promise<void> {
  const response = await fetch(pricesUrl(lat, lon), { signal })
  if (!response.ok || !response.body) throw new Error(`Prices stream failed: ${response.status}`)

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
      if (line.trim().length > 0) onEvent(JSON.parse(line) as PriceStreamEvent)
    }
  }
  if (buffer.trim().length > 0) onEvent(JSON.parse(buffer) as PriceStreamEvent)
}
