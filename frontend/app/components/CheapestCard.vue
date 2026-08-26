<script setup lang="ts">
import type { RangedStore } from '../composables/useStorePrices'
import { ago, eur } from '../composables/useStorePrices'

const props = defineProps<{
  best: RangedStore | null
  areaAvg: number | null
}>()

const emit = defineEmits<{ show: [] }>()

const savings = computed(() => {
  if (!props.best || !props.areaAvg) return '—'
  return `€${(props.areaAvg - props.best.low).toFixed(2)} under area average`
})

const priceLabel = computed(() => (props.best ? eur(props.best.low) : '--,--'))
const storeLabel = computed(() => props.best?.brand ?? 'no data')
const distLabel = computed(() => (props.best ? `${props.best.dist.toFixed(1)} km` : 'no data'))

const glowStyle = computed(() => ({
  position: 'absolute' as const,
  inset: 0,
  pointerEvents: 'none' as const,
  background: props.best
    ? 'radial-gradient(120% 140% at 0% 0%, color-mix(in srgb, var(--color-accent-400) 12%, transparent) 0%, transparent 62%)'
    : 'transparent',
}))
</script>

<template>
  <div class="card">
    <div :style="glowStyle" aria-hidden="true" />
    <div class="kicker">Cheapest can near you</div>
    <div class="price-row">
      <div class="price">{{ priceLabel }}</div>
      <div class="price-meta">
        <div class="per-can">per 500&nbsp;ml can</div>
        <div class="pack">{{ best?.pack ?? '' }}</div>
      </div>
    </div>
    <div class="store-row">
      <a v-if="best?.sourceUrl" class="store-name store-link" :href="best.sourceUrl" target="_blank" rel="noopener noreferrer">{{ best.brand }}</a>
      <span v-else class="store-name">{{ storeLabel }}</span>
      <span class="store-meta">
        {{ distLabel }}<template v-if="best?.name"> · {{ best.name }}</template>
      </span>
    </div>
    <div class="tags-row">
      <span class="tag tag-accent">{{ savings }}</span>
      <span class="ago">{{ best ? ago(best.latestFetchedAt) : '' }}</span>
    </div>
    <button class="btn btn-primary btn-block" :disabled="!best" @click="emit('show')">
      <i class="ph ph-navigation-arrow" />Show on map
    </button>
  </div>
</template>

<style scoped>
.card {
  flex: none;
  position: relative;
  padding: var(--space-6);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 86%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
  overflow: hidden;
}
.kicker {
  font-size: 11px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-accent-400);
  margin-bottom: var(--space-4);
}
.price-row {
  display: flex;
  align-items: flex-start;
  gap: var(--space-4);
}
.price {
  font-family: var(--font-heading);
  font-weight: 500;
  font-size: 68px;
  line-height: 0.85;
  letter-spacing: -0.04em;
}
.price-meta {
  padding-top: 4px;
}
.per-can {
  font-size: 12px;
  color: var(--color-neutral-500);
}
.pack {
  margin-top: 4px;
  font-size: 12px;
  color: var(--color-accent-300);
}
.store-row {
  margin-top: var(--space-6);
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
}
.store-name {
  font-family: var(--font-heading);
  font-size: 17px;
}
.store-link {
  color: inherit;
  text-decoration: none;
}
.store-link:hover {
  text-decoration: underline;
}
.store-meta {
  font-size: 12px;
  color: var(--color-neutral-500);
}
.tags-row {
  margin-top: var(--space-3);
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
}
.tags-row .tag {
  font-size: 10px;
}
.ago {
  font-size: 11px;
  color: var(--color-neutral-600);
}
</style>
