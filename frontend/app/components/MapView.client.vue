<script setup lang="ts">
import type { Map as LeafletMap, LayerGroup, Circle, Marker } from 'leaflet'
import type { RangedStore } from '../composables/useStorePrices'
import { eur } from '../composables/useStorePrices'

const props = defineProps<{
  lat: number
  lon: number
  radiusKm: number
  stores: RangedStore[]
}>()

const mapEl = ref<HTMLDivElement | null>(null)
let L: typeof import('leaflet')
let map: LeafletMap | null = null
let markerLayer: LayerGroup | null = null
let ring: Circle | null = null
let meMarker: Marker | null = null

function variantColorFor(store: RangedStore) {
  return store.lowVariant.color
}

function variantDimFor(store: RangedStore) {
  return store.lowVariant.dim
}

async function paintMarkers(refit: boolean) {
  if (!map || !markerLayer) return
  markerLayer.clearLayers()

  const best = props.stores[0]
  ring?.setLatLng([props.lat, props.lon])
  ring?.setRadius(props.radiusKm * 1000)
  meMarker?.setLatLng([props.lat, props.lon])

  for (const store of props.stores) {
    const isBest = best && store.id === best.id
    const style = isBest
      ? `background:${variantColorFor(store)};border-color:${variantColorFor(store)};color:#161826;font-weight:600;`
      : `border-color:${variantDimFor(store)};color:${variantColorFor(store)};`

    const marker = L.marker([store.lat, store.lon], {
      icon: L.divIcon({
        className: '',
        iconSize: [48, 22],
        iconAnchor: [24, 11],
        html: `<div class="mb-pin${isBest ? ' best' : ''}" style="${style}">${eur(store.low)}</div>`,
      }),
    })
    marker.bindPopup(
      `<b>${store.brand}</b>${store.name ? `<br>${store.name}` : ''}<br>${eur(store.low)} · ${store.lowVariant.name} · ${store.dist.toFixed(1)} km`,
    )
    marker.addTo(markerLayer)
  }

  if (refit) fitToRange()
}

function fitToRange() {
  if (!map || !L) return
  const pts: [number, number][] = props.stores.map((s) => [s.lat, s.lon] as [number, number])
  pts.push([props.lat, props.lon])

  let minLat = pts[0]![0], maxLat = pts[0]![0], minLon = pts[0]![1], maxLon = pts[0]![1]
  for (const [la, lo] of pts) {
    minLat = Math.min(minLat, la); maxLat = Math.max(maxLat, la)
    minLon = Math.min(minLon, lo); maxLon = Math.max(maxLon, lo)
  }
  const eps = 0.004
  if (maxLat - minLat < eps) { minLat -= eps; maxLat += eps }
  if (maxLon - minLon < eps) { minLon -= eps; maxLon += eps }
  if (![minLat, maxLat, minLon, maxLon].every(Number.isFinite)) return

  const bounds = L.latLngBounds(L.latLng(minLat, minLon), L.latLng(maxLat, maxLon))
  const size = map.getSize()
  const left = Math.min(430, Math.round(size.x * 0.42))
  const pad = L.point(left + 40, 140)
  const zoom = Math.min(14, map.getBoundsZoom(bounds, false, pad))
  const center = bounds.getCenter()
  const shift = map.project(center, zoom).subtract(L.point((left - 40) / 2, 0))
  map.setView(map.unproject(shift, zoom), zoom, { animate: false })
}

function focus(store: { lat: number, lon: number }) {
  map?.setView([store.lat, store.lon], 15, { animate: true })
}

defineExpose({ focus })

onMounted(async () => {
  L = await import('leaflet')
  if (!mapEl.value) return

  map = L.map(mapEl.value, { zoomControl: false, attributionControl: true }).setView([props.lat, props.lon], 12)
  L.control.zoom({ position: 'bottomright' }).addTo(map)

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

  map.whenReady(() => {
    if (!map) return
    map.invalidateSize()
    ring = L.circle([props.lat, props.lon], {
      radius: props.radiusKm * 1000,
      color: '#968ae0',
      weight: 1,
      opacity: 0.45,
      fillColor: '#9184d9',
      fillOpacity: 0.05,
    }).addTo(map)
    meMarker = L.marker([props.lat, props.lon], {
      zIndexOffset: 1000,
      icon: L.divIcon({ className: '', iconSize: [14, 14], iconAnchor: [7, 7], html: '<div class="mb-me"></div>' }),
    }).addTo(map).bindPopup('You are here')
    paintMarkers(true)
  })
})

onBeforeUnmount(() => {
  map?.remove()
  map = null
})

let lastFitKey = ''
watch(
  () => [props.lat, props.lon, props.radiusKm, props.stores.map((s) => s.id).join(',')],
  () => {
    const key = `${props.lat},${props.lon},${props.radiusKm}`
    const moved = key !== lastFitKey
    lastFitKey = key
    paintMarkers(moved)
  },
)
</script>

<template>
  <div ref="mapEl" style="position: absolute; inset: 0; z-index: 0;" />
</template>
