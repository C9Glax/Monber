<script setup lang="ts">
import { MAX_RADIUS_KM } from './composables/useStorePrices'
import { poiStoresUrl, pricesUrl } from './composables/useMonberApi'
import { geocodeAddress } from './composables/useGeocoding'

const RADIUS_PRESETS = [1, 2, 5]

const lat = ref(52.5200)
const lon = ref(13.4050)
const placeLabel = ref('Berlin Mitte, DE')
const radiusKm = ref(5)
const locating = ref(false)
const geocoding = ref(false)
const geocodeError = ref<string | null>(null)

const { loading, error, refresh, inRange, inRangeFuture, unpricedInRange } = useStorePrices()

const mapView = ref<{ focus: (s: { lat: number, lon: number }) => void } | null>(null)

const rangeStores = computed(() => inRange(radiusKm.value))
const futureRangeStores = computed(() => inRangeFuture(radiusKm.value))
const unpricedStores = computed(() => unpricedInRange(radiusKm.value))

const best = computed(() => rangeStores.value[0] ?? null)
const futureBest = computed(() => futureRangeStores.value[0] ?? null)
const areaAvg = computed(() => {
  const lows = rangeStores.value.map((s) => s.low)
  if (lows.length === 0) return null
  return lows.reduce((a, b) => a + b, 0) / lows.length
})

const poiUrl = computed(() => poiStoresUrl(lat.value, lon.value))
const pricesApiUrl = computed(() => pricesUrl(lat.value, lon.value))

function showBestOnMap() {
  if (best.value) mapView.value?.focus(best.value)
}

function showFutureBestOnMap() {
  if (futureBest.value) mapView.value?.focus(futureBest.value)
}

function selectStore(store: { lat: number, lon: number }) {
  mapView.value?.focus(store)
}

function onMapLocationClick(clickLat: number, clickLon: number) {
  lat.value = clickLat
  lon.value = clickLon
  placeLabel.value = 'Custom location'
}

async function searchAddress(query: string) {
  geocoding.value = true
  geocodeError.value = null
  try {
    const result = await geocodeAddress(query)
    if (result) {
      lat.value = result.lat
      lon.value = result.lon
      placeLabel.value = result.label
    }
    else {
      geocodeError.value = 'Address not found'
    }
  }
  catch {
    geocodeError.value = 'Could not reach the geocoding service'
  }
  finally {
    geocoding.value = false
  }
}

function locate() {
  if (!navigator.geolocation) return
  locating.value = true
  navigator.geolocation.getCurrentPosition(
    (pos) => {
      lat.value = pos.coords.latitude
      lon.value = pos.coords.longitude
      placeLabel.value = 'Your location'
      locating.value = false
    },
    () => {
      locating.value = false
    },
  )
}

watch([lat, lon], () => refresh(lat.value, lon.value), { immediate: true })
</script>

<template>
  <div class="app-root">
    <MapView
      ref="mapView"
      :lat="lat"
      :lon="lon"
      :radius-km="radiusKm"
      :stores="rangeStores"
      :unpriced-stores="unpricedStores"
      @location-click="onMapLocationClick"
    />

    <div class="overlay">
      <BrandBar />

      <ControlsBar
        v-model:place-label="placeLabel"
        v-model:radius-km="radiusKm"
        :radius-max="MAX_RADIUS_KM"
        :radius-presets="RADIUS_PRESETS"
        :locating="locating"
        :geocoding="geocoding"
        :geocode-error="geocodeError"
        @locate="locate"
        @search="searchAddress"
      />

      <div class="sidebar">
        <div v-if="error" class="error-card">{{ error }}</div>
        <div v-else-if="loading" class="loading-card">Loading nearby stores…</div>
        <template v-else>
          <CheapestCard :best="best" :area-avg="areaAvg" @show="showBestOnMap" />
          <FutureLowestCard :best="futureBest" @show="showFutureBestOnMap" />
          <StoreList :rows="rangeStores" :area-avg="areaAvg" @select="selectStore" />
        </template>

        <ApiDebugLine :poi-url="poiUrl" :prices-url="pricesApiUrl" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.app-root {
  position: fixed;
  inset: 0;
  overflow: hidden;
}
.overlay {
  position: absolute;
  inset: 0;
  z-index: 1000;
  pointer-events: none;
  display: grid;
  grid-template-columns: 392px 1fr auto;
  grid-template-rows: auto 1fr;
  gap: var(--space-6);
  padding: var(--space-6);
}
.sidebar {
  grid-column: 1 / 2;
  grid-row: 2;
  pointer-events: auto;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
.error-card,
.loading-card {
  flex: none;
  padding: var(--space-4) var(--space-6);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 86%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
  font-size: 13px;
  color: var(--color-neutral-400);
}
.error-card {
  color: var(--color-accent-300);
}
</style>
