<script setup lang="ts">
import type { Map as LeafletMap, LayerGroup, Circle, Marker, LatLng } from 'leaflet'
import type { MergedStore, RangedStore } from '../composables/useStorePrices'
import { eur } from '../composables/useStorePrices'

const props = defineProps<{
  lat: number
  lon: number
  radiusKm: number
  stores: RangedStore[]
  /** Stores not yet checked by the price stream - shown with a loading marker. */
  pendingStores: MergedStore[]
  /** Stores confirmed to have no price data. */
  emptyStores: MergedStore[]
}>()

const emit = defineEmits<{ locationClick: [lat: number, lon: number], storeSelect: [id: number] }>()

const mapEl = ref<HTMLDivElement | null>(null)
let L: typeof import('leaflet')
let map: LeafletMap | null = null
let markerLayer: LayerGroup | null = null
let ring: Circle | null = null
let meMarker: Marker | null = null

async function paintMarkers(refit: boolean) {
  if (!map || !markerLayer) return
  markerLayer.clearLayers()

  const best = props.stores[0]
  ring?.setLatLng([props.lat, props.lon])
  ring?.setRadius(props.radiusKm * 1000)
  meMarker?.setLatLng([props.lat, props.lon])

  for (const store of props.stores) {
    const isBest = best && store.id === best.id
    const isSelected = store.id === selectedId
    const style = isBest
      ? 'background:var(--color-accent-400);border-color:var(--color-accent-400);color:#161826;font-weight:600;'
      : 'border-color:var(--color-neutral-700);color:var(--color-accent-300);'
    const pinClass = ['mb-pin', isBest && 'best', isSelected && 'selected'].filter(Boolean).join(' ')

    const marker = L.marker([store.lat, store.lon], {
      zIndexOffset: isSelected ? 500 : 0,
      icon: L.divIcon({
        className: '',
        iconSize: [48, 22],
        iconAnchor: [24, 11],
        html: `<div class="${pinClass}" style="${style}">${eur(store.low)}</div>`,
      }),
    })
    marker.bindPopup(
      `<b>${store.brand}</b>${store.name ? `<br>${store.name}` : ''}<br>${eur(store.low)} · ${store.pack} · ${store.dist.toFixed(1)} km`,
    )
    marker.on('click', () => {
      selectedId = store.id
      emit('storeSelect', store.id)
      paintMarkers(false)
    })
    marker.addTo(markerLayer)
  }

  for (const store of props.pendingStores) {
    const marker = L.marker([store.lat, store.lon], {
      zIndexOffset: -1000,
      icon: L.divIcon({ className: '', iconSize: [12, 12], iconAnchor: [6, 6], html: '<div class="mb-pending"></div>' }),
    })
    marker.bindPopup(`<b>${store.brand}</b>${store.name ? `<br>${store.name}` : ''}<br>Checking price…`)
    marker.on('click', () => {
      selectedId = store.id
      emit('storeSelect', store.id)
      paintMarkers(false)
    })
    marker.addTo(markerLayer)
  }

  for (const store of props.emptyStores) {
    const marker = L.marker([store.lat, store.lon], {
      zIndexOffset: -1000,
      icon: L.divIcon({ className: '', iconSize: [9, 9], iconAnchor: [4, 4], html: '<div class="mb-empty"></div>' }),
    })
    marker.bindPopup(`<b>${store.brand}</b>${store.name ? `<br>${store.name}` : ''}<br>No price data`)
    marker.on('click', () => {
      selectedId = store.id
      emit('storeSelect', store.id)
      paintMarkers(false)
    })
    marker.addTo(markerLayer)
  }

  if (refit) fitToRange()
}

/** Pure geometry: computes the fit-to-range center/zoom without touching the map's view. */
function computeFitView(): { center: LatLng, zoom: number } | null {
  if (!map || !L) return null
  const pts: [number, number][] = props.stores.map((s) => [s.lat, s.lon] as [number, number])
  pts.push([props.lat, props.lon])

  let minLat = pts[0]![0], maxLat = pts[0]![0], minLon = pts[0]![1], maxLon = pts[0]![1]
  for (const [la, lo] of pts) {
    minLat = Math.min(minLat, la); maxLat = Math.max(maxLat, la)
    minLon = Math.min(minLon, lo); maxLon = Math.max(maxLon, lo)
  }
  if (![minLat, maxLat, minLon, maxLon].every(Number.isFinite)) return null

  // Always cover the full selected radius around the user, even with zero (priced) stores to
  // bound around - otherwise the map zoomed in tight on just the user's own point regardless of
  // the radius setting, leaving most unpriced-store markers correctly placed but off-screen.
  const bounds = L.latLngBounds(L.latLng(minLat, minLon), L.latLng(maxLat, maxLon))
    .extend(L.latLng(props.lat, props.lon).toBounds(props.radiusKm * 1000 * 2))
  const size = map.getSize()
  const left = Math.min(430, Math.round(size.x * 0.42))
  const pad = L.point(left + 40, 140)
  const zoom = Math.min(14, map.getBoundsZoom(bounds, false, pad))
  const center = bounds.getCenter()
  const shift = map.project(center, zoom).subtract(L.point((left - 40) / 2, 0))
  return { center: map.unproject(shift, zoom), zoom }
}

// Tracks whether the user has manually panned/zoomed the map, so subsequent radius/data
// changes don't yank the view back to the auto-fit bounds. suppressInteraction distinguishes
// our own programmatic setView calls (which also fire movestart/zoomstart) from real user
// interaction (drag, scroll-zoom, pinch, keyboard pan) picked up by the map's event listener.
let userInteracted = false
let suppressInteraction = false

function programmaticSetView(center: LatLng | [number, number], zoom: number, opts?: { animate?: boolean }) {
  if (!map) return
  suppressInteraction = true
  map.setView(center, zoom, opts)
  suppressInteraction = false
}

function fitToRange() {
  const view = computeFitView()
  if (view) programmaticSetView(view.center, view.zoom, { animate: false })
}

let selectedId: number | null = null

function focus(store: { id?: number, lat: number, lon: number }) {
  // An explicit "show on map" / store click is itself a deliberate view change - treat it like
  // manual interaction so a later radius tweak doesn't yank the view away from what was asked for.
  userInteracted = true
  selectedId = store.id ?? null
  programmaticSetView([store.lat, store.lon], 15, { animate: true })
  paintMarkers(false)
}

defineExpose({ focus })

onMounted(async () => {
  L = await import('leaflet')
  if (!mapEl.value) return

  map = L.map(mapEl.value, { zoomControl: false, attributionControl: true })
  map.on('movestart zoomstart', () => {
    if (!suppressInteraction) userInteracted = true
  })
  // Clicking the base map moves the search center there. Interactive layers (store markers,
  // popups) stop this event from reaching the map on their own, so this only fires for clicks
  // on open map area - see also the ring circle's `interactive: false` below, which would
  // otherwise swallow clicks anywhere inside the radius circle.
  map.on('click', (e) => emit('locationClick', e.latlng.lat, e.latlng.lng))
  programmaticSetView([props.lat, props.lon], 12, { animate: false })
  L.control.zoom({ position: 'bottomright' }).addTo(map)
  map.invalidateSize()

  // Settle on the final (fit-to-range) view BEFORE adding the tile layer, so tiles are only
  // ever requested once, at the correct zoom. Setting the view a second time right after tiles
  // for the first (temporary) view had already started loading left stale tiles from the
  // abandoned zoom level rendered alongside the new ones - reported as tiles that only line up
  // correctly on one axis, since each generation's tile grid has its own pixel origin.
  const initialView = computeFitView()
  if (initialView) programmaticSetView(initialView.center, initialView.zoom, { animate: false })
  lastLatLon = `${props.lat},${props.lon}`
  lastFitKey = `${props.lat},${props.lon},${props.radiusKm}`

  const mapTilerKey = useRuntimeConfig().public.mapTilerKey
  if (mapTilerKey) {
    // Leaflet's `{r}` placeholder already resolves to "@2x" on retina displays independent of
    // any option, matching MapTiler's real @2x tiles at the same zoom/tile size. Do NOT also
    // set detectRetina: true - that's a *different* mechanism (for providers without real @2x
    // tiles) that fetches regular tiles one zoom level deeper and halves their displayed size;
    // combined with real @2x tiles it double-compensates, squeezing 512px images into 128px
    // boxes at the wrong zoom level, which is what caused the misaligned/blurry tile seams.
    L.tileLayer(`https://api.maptiler.com/maps/dataviz-dark/256/{z}/{x}/{y}{r}.png?key=${mapTilerKey}`, {
      attribution: '© OpenStreetMap · © MapTiler',
      maxZoom: 19,
    }).addTo(map)
  }
  else {
    // No MapTiler key configured - fall back to plain (light) OSM tiles rather than a broken map.
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
      maxZoom: 19,
    }).addTo(map)
  }

  markerLayer = L.layerGroup().addTo(map)
  ring = L.circle([props.lat, props.lon], {
    radius: props.radiusKm * 1000,
    color: '#968ae0',
    weight: 1,
    opacity: 0.45,
    fillColor: '#9184d9',
    fillOpacity: 0.05,
    interactive: false,
  }).addTo(map)
  // Not interactive: this marker is small and often sits right on top of (or very near) a
  // store's price pin - being interactive with a high z-index meant it silently swallowed
  // clicks meant for the store marker underneath, so "click a marker" would show "You are
  // here" instead of the store's price even though the store pin was what was visually
  // clicked. The accent-colored dot plus the "Your location"/address label elsewhere already
  // communicate the user's position without needing its own click target.
  meMarker = L.marker([props.lat, props.lon], {
    zIndexOffset: 400,
    interactive: false,
    icon: L.divIcon({ className: '', iconSize: [14, 14], iconAnchor: [7, 7], html: '<div class="mb-me"></div>' }),
  }).addTo(map)

  paintMarkers(false)
})

onBeforeUnmount(() => {
  if (repaintTimer) clearTimeout(repaintTimer)
  map?.remove()
  map = null
})

let lastLatLon = ''
let lastFitKey = ''
let repaintTimer: ReturnType<typeof setTimeout> | null = null
watch(
  () => [
    props.lat,
    props.lon,
    props.radiusKm,
    props.stores.map((s) => s.id).join(','),
    props.pendingStores.map((s) => s.id).join(','),
    props.emptyStores.map((s) => s.id).join(','),
  ],
  () => {
    // Debounced: the radius slider/number input fires on every tick while dragging, and each
    // call here can trigger map.setView() via fitToRange(). Calling setView in a rapid burst -
    // faster than Leaflet's async tile loading/pruning for the previous call can keep up with -
    // leaves orphaned tiles from stale zoom generations rendered alongside new ones, which is
    // what showed up as tiles rendered at seemingly unrelated/overlapping positions. Coalescing
    // rapid-fire updates into one, after the burst settles, avoids flooding Leaflet's tile layer.
    if (repaintTimer) clearTimeout(repaintTimer)
    repaintTimer = setTimeout(() => {
      repaintTimer = null

      const latLonKey = `${props.lat},${props.lon}`
      const locationChanged = latLonKey !== lastLatLon
      lastLatLon = latLonKey
      // A genuinely new location (e.g. "Locate me") always deserves a fresh, centered view,
      // even if the user had manually panned/zoomed away from a previous auto-fit.
      if (locationChanged) userInteracted = false

      const key = `${latLonKey},${props.radiusKm}`
      const dataChanged = key !== lastFitKey
      lastFitKey = key

      // Once the user has manually panned/zoomed (or clicked a store to focus it), don't yank
      // the view back just because the radius changed - only a genuinely new location should
      // move the map from then on.
      const moved = dataChanged && (locationChanged || !userInteracted)
      paintMarkers(moved)
    }, 150)
  },
)
</script>

<template>
  <div ref="mapEl" style="position: absolute; inset: 0; z-index: 0;" />
</template>
