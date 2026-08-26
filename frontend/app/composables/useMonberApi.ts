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

function apiBase(): string {
  return useRuntimeConfig().public.apiBase
}

export function poiStoresUrl(lat: number, lon: number): string {
  return `${apiBase()}/poi/stores?lat=${lat}&lon=${lon}`
}

export function pricesUrl(lat: number, lon: number): string {
  return `${apiBase()}/prices/prices?lat=${lat}&lon=${lon}`
}

export async function fetchPoiStores(lat: number, lon: number): Promise<PoiStore[]> {
  return await $fetch<PoiStore[]>(poiStoresUrl(lat, lon))
}

export async function fetchPrices(lat: number, lon: number): Promise<PriceObservation[]> {
  return await $fetch<PriceObservation[]>(pricesUrl(lat, lon))
}
