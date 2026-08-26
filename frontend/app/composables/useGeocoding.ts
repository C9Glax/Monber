export interface GeocodeResult {
  lat: number
  lon: number
  label: string
}

/**
 * Resolves a free-text address to coordinates via Nominatim (OpenStreetMap's public geocoder).
 * No API key needed, but Nominatim's usage policy expects a low request volume and an
 * identifiable client - the browser's own Referer header covers that for this app.
 */
export async function geocodeAddress(query: string): Promise<GeocodeResult | null> {
  const trimmed = query.trim()
  if (!trimmed) return null

  const url = `https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${encodeURIComponent(trimmed)}`
  const results = await $fetch<Array<{ lat: string, lon: string, display_name: string }>>(url)
  if (!results.length) return null

  const r = results[0]!
  return { lat: Number.parseFloat(r.lat), lon: Number.parseFloat(r.lon), label: r.display_name }
}
