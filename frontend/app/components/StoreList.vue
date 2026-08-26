<script setup lang="ts">
import type { RangedStore } from '../composables/useStorePrices'
import { ago, eur } from '../composables/useStorePrices'

const props = defineProps<{
  rows: RangedStore[]
  areaAvg: number | null
  dealThreshold?: number
}>()

const emit = defineEmits<{ select: [store: RangedStore] }>()

function isDeal(store: RangedStore): boolean {
  if (!props.areaAvg) return false
  return store.low < props.areaAvg - (props.dealThreshold ?? 0.05)
}
</script>

<template>
  <div class="card">
    <div class="header">
      <span class="heading">Stores in range · {{ rows.length }}</span>
      <span class="sub">lowest first</span>
    </div>
    <div class="mb-scroll list">
      <div
        v-for="row in rows"
        :key="row.id"
        class="row"
        @click="emit('select', row)"
      >
        <div class="bar" aria-hidden="true" />
        <div class="brand-line">
          <span class="brand">{{ row.brand }}</span>
          <span class="dist">{{ row.dist.toFixed(1) }} km</span>
          <span v-if="isDeal(row)" class="tag tag-accent deal-tag">deal</span>
        </div>
        <div class="price">{{ eur(row.low) }}</div>
        <div class="detail">
          <template v-if="row.name">{{ row.name }} · </template>{{ row.pack }}
        </div>
        <div class="ago">{{ ago(row.latestFetchedAt) }}</div>
      </div>
      <div v-if="rows.length === 0" class="empty">No priced stores in range.</div>
    </div>
  </div>
</template>

<style scoped>
.card {
  flex: 1 1 auto;
  min-height: 160px;
  display: flex;
  flex-direction: column;
  padding: var(--space-4) var(--space-4) var(--space-4);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 86%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
}
.header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-4);
  padding: 0 var(--space-2) var(--space-4);
}
.heading {
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-neutral-600);
}
.sub {
  font-size: 11px;
  color: var(--color-neutral-700);
}
.list {
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  padding-right: var(--space-1);
}
.row {
  display: grid;
  grid-template-columns: 2px 1fr auto;
  gap: var(--space-1) var(--space-3);
  padding: var(--space-3) var(--space-4);
  border-radius: var(--radius-md);
  border: 1px solid transparent;
  cursor: pointer;
}
.row:hover {
  border-color: var(--color-accent-700);
  background: color-mix(in srgb, var(--color-accent-900) 60%, transparent);
}
.bar {
  grid-row: 1 / 3;
  border-radius: 999px;
  opacity: 0.85;
  background: var(--color-accent-500);
}
.brand-line {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}
.brand {
  font-family: var(--font-heading);
  font-size: 14px;
}
.dist {
  font-size: 11px;
  color: var(--color-neutral-500);
}
.deal-tag {
  font-size: 9px;
  padding: 1px 6px;
}
.price {
  text-align: right;
  font-family: var(--font-heading);
  font-size: 15px;
}
.detail {
  grid-column: 2;
  font-size: 11px;
  color: var(--color-neutral-600);
}
.ago {
  text-align: right;
  font-size: 10px;
  color: var(--color-neutral-700);
}
.empty {
  padding: var(--space-4);
  font-size: 12px;
  color: var(--color-neutral-600);
}
</style>
