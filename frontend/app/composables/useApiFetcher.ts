import createClient from 'openapi-fetch'
import type { paths as PoiPaths } from '~/types/api/poi'
import type { paths as PricesPaths } from '~/types/api/prices'

function apiBase(): string {
  return useRuntimeConfig().public.apiBase
}

/**
 * Typed `openapi-fetch` client for Services.POI - paths, query params and response shapes come
 * from `~/types/api/poi`, generated from the service's own OpenAPI document (see
 * frontend/package.json's `generate:api` script). Run that script after changing a POI route or
 * DTO so this client's types stay in sync with the backend.
 */
export function usePoiApi() {
  return createClient<PoiPaths>({ baseUrl: `${apiBase()}/poi` })
}

/** Same as {@link usePoiApi}, for Services.Prices (`~/types/api/prices`). */
export function usePricesApi() {
  return createClient<PricesPaths>({ baseUrl: `${apiBase()}/prices` })
}
