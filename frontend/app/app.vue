<script setup lang="ts">
import type { VariantDef } from './composables/useStorePrices'
import { MAX_RADIUS_KM } from './composables/useStorePrices'
import { poiStoresUrl, pricesUrl } from './composables/useMonberApi'

const RADIUS_PRESETS = [5, 10, 20, 30]

const lat = ref(52.5200)
const lon = ref(13.4050)
const placeLabel = ref('Berlin Mitte, DE')
const radiusKm = ref(10)
const selectedVariant = ref<VariantDef['key'] | null>(null)
const locating = ref(false)

const { loading, error, refresh, inRange } = useStorePrices()

const mapView = ref<{ focus: (s: { lat: number, lon: number }) => void } | null>(null)

const rangeStores = computed(() => inRange(radiusKm.value, selectedVariant.value))
const allRangeStores = computed(() => inRange(radiusKm.value, null))

const best = computed(() => rangeStores.value[0] ?? null)
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

function selectStore(store: { lat: number, lon: number }) {
  mapView.value?.focus(store)
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
    <MapView ref="mapView" :lat="lat" :lon="lon" :radius-km="radiusKm" :stores="rangeStores" />

    <div class="overlay">
      <BrandBar />

      <ControlsBar
        :place-label="placeLabel"
        v-model:radius-km="radiusKm"
        :radius-max="MAX_RADIUS_KM"
        :radius-presets="RADIUS_PRESETS"
        :locating="locating"
        @locate="locate"
      />

      <div class="sidebar">
        <div v-if="error" class="error-card">{{ error }}</div>
        <div v-else-if="loading" class="loading-card">Loading nearby stores…</div>
        <template v-else>
          <CheapestCard :best="best" :area-avg="areaAvg" @show="showBestOnMap" />
          <VariantList :all-range-stores="allRangeStores" v-model:selected="selectedVariant" />
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
