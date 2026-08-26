<script setup lang="ts">
const props = defineProps<{
  placeLabel: string
  radiusKm: number
  radiusMax: number
  radiusPresets: number[]
  locating: boolean
  geocoding: boolean
  geocodeError: string | null
}>()

const emit = defineEmits<{
  'update:radiusKm': [value: number]
  'update:placeLabel': [value: string]
  locate: []
  search: [query: string]
}>()

function setRadius(v: number | string) {
  const r = Math.max(1, Math.min(props.radiusMax, Math.round(Number(v) || 0)))
  emit('update:radiusKm', r)
}

function onSliderInput(e: Event) {
  setRadius((e.target as HTMLInputElement).value)
}

function onPlaceInput(e: Event) {
  emit('update:placeLabel', (e.target as HTMLInputElement).value)
}

function onSearch() {
  emit('search', props.placeLabel)
}
</script>

<template>
  <div class="controls-bar">
    <form class="place-search" @submit.prevent="onSearch">
      <i class="ph ph-map-pin" style="color: var(--color-accent);" />
      <input
        type="text"
        :value="placeLabel"
        placeholder="Search address…"
        :disabled="geocoding"
        aria-label="Search address"
        @input="onPlaceInput"
      >
    </form>
    <span v-if="geocodeError" class="geocode-error">{{ geocodeError }}</span>
    <span class="divider" />
    <div class="radius-group">
      <div class="presets">
        <button
          v-for="preset in radiusPresets"
          :key="preset"
          class="btn preset-btn"
          :class="preset === radiusKm ? 'preset-active' : 'btn-ghost'"
          @click="setRadius(preset)"
        >
          {{ preset }} km
        </button>
      </div>
      <div class="slider-row">
        <input
          type="range"
          min="1"
          :max="radiusMax"
          step="1"
          :value="radiusKm"
          aria-label="Search radius"
          @input="onSliderInput"
        >
        <span class="radius-input">
          <input
            type="number"
            min="1"
            :max="radiusMax"
            step="1"
            :value="radiusKm"
            aria-label="Search radius in kilometres"
            @input="onSliderInput"
          >
          <span class="unit">km</span>
        </span>
      </div>
    </div>
    <span class="divider" />
    <button class="btn btn-ghost" :disabled="locating" @click="emit('locate')">
      <i class="ph ph-crosshair" />{{ locating ? 'Locating…' : 'Locate me' }}
    </button>
  </div>
</template>

<style scoped>
.controls-bar {
  grid-column: 3;
  grid-row: 1;
  pointer-events: auto;
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-items: center;
  max-width: calc(100vw - 392px - 4 * var(--space-6));
  gap: var(--space-2) var(--space-3);
  padding: var(--space-3) var(--space-3) var(--space-3) var(--space-4);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 82%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
}
.place-search {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  white-space: nowrap;
  color: var(--color-neutral-300);
}
.place-search input {
  width: 160px;
  background: transparent;
  border: 0;
  color: var(--color-text);
  font-family: var(--font-heading);
  font-size: 13px;
  padding: 0;
}
.place-search input::placeholder {
  color: var(--color-neutral-500);
}
.geocode-error {
  font-size: 11px;
  color: var(--color-accent-300);
  white-space: nowrap;
}
.divider {
  width: 1px;
  height: 20px;
  background: var(--color-neutral-800);
}
.radius-group {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: var(--space-2);
}
.presets {
  display: flex;
  gap: 2px;
  justify-content: center;
}
.preset-btn {
  font-size: 12px;
  padding: 5px 11px;
  white-space: nowrap;
  border-radius: var(--radius-sm);
}
.preset-active {
  border: 1px solid var(--color-accent-600);
  background: color-mix(in srgb, var(--color-accent-800) 70%, transparent);
  color: var(--color-accent-200);
}
.slider-row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}
.slider-row input[type='range'] {
  width: 108px;
  accent-color: var(--color-accent);
}
.radius-input {
  display: inline-flex;
  align-items: baseline;
  gap: 4px;
  padding: 3px 8px;
  border: 1px solid var(--color-neutral-800);
  border-radius: var(--radius-sm);
}
.radius-input input {
  width: 40px;
  background: transparent;
  border: 0;
  color: var(--color-text);
  font-family: var(--font-heading);
  font-size: 13px;
  padding: 0;
}
.radius-input .unit {
  font-size: 11px;
  color: var(--color-neutral-500);
}
</style>
