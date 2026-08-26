<script setup lang="ts">
import type { RangedStore, VariantDef } from '../composables/useStorePrices'
import { VARIANTS, eur } from '../composables/useStorePrices'

const props = defineProps<{
  allRangeStores: RangedStore[]
  selected: VariantDef['key'] | null
}>()

const emit = defineEmits<{ 'update:selected': [key: VariantDef['key'] | null] }>()

interface Row {
  variant: VariantDef
  price: number | null
  storeName: string
  distance: string
  selected: boolean
}

const rows = computed<Row[]>(() =>
  VARIANTS.map((variant) => {
    const withPrice = props.allRangeStores
      .filter((s) => s.prices[variant.key] != null)
      .sort((a, b) => (a.prices[variant.key] as number) - (b.prices[variant.key] as number))
    const low = withPrice[0]
    return {
      variant,
      price: low ? (low.prices[variant.key] as number) : null,
      storeName: low ? low.brand : 'no stock nearby',
      distance: low ? `${low.dist.toFixed(1)} km` : '',
      selected: props.selected === variant.key,
    }
  }),
)

function toggle(key: VariantDef['key']) {
  emit('update:selected', props.selected === key ? null : key)
}

const filterLabel = computed(() => VARIANTS.find((v) => v.key === props.selected)?.name ?? '')
</script>

<template>
  <div class="card">
    <div class="heading">By variant</div>
    <button
      v-for="row in rows"
      :key="row.variant.key"
      class="row"
      :class="{ selected: row.selected }"
      :style="{
        borderColor: row.selected ? row.variant.dim : 'transparent',
        background: row.selected ? `color-mix(in srgb, ${row.variant.color} 10%, transparent)` : 'transparent',
      }"
      @click="toggle(row.variant.key)"
    >
      <span class="dot" :style="{ background: row.variant.color }" />
      <span class="name">{{ row.variant.name }}</span>
      <span class="price" :style="{ color: row.variant.color }">{{ eur(row.price) }}</span>
      <span class="meta">{{ row.storeName }}<template v-if="row.distance"> · {{ row.distance }}</template></span>
    </button>
    <button v-if="selected" class="btn btn-ghost clear-btn" @click="emit('update:selected', null)">
      <i class="ph ph-x" />Showing {{ filterLabel }} only — clear
    </button>
  </div>
</template>

<style scoped>
.card {
  flex: none;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4) var(--space-6) var(--space-6);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 86%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
}
.heading {
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-neutral-600);
  padding: var(--space-2) 0 var(--space-3);
}
.row {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-3);
  cursor: pointer;
  border-radius: var(--radius-md);
  font: inherit;
  color: inherit;
  border: 1px solid transparent;
  border-top-color: var(--color-neutral-900);
  background: transparent;
  text-align: left;
}
.row:hover {
  border-color: var(--color-neutral-700);
}
.dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  flex: none;
}
.name {
  font-family: var(--font-heading);
  font-size: 14px;
  min-width: 92px;
  text-align: left;
}
.price {
  font-family: var(--font-heading);
  font-size: 19px;
  letter-spacing: -0.02em;
}
.meta {
  margin-left: auto;
  font-size: 11px;
  color: var(--color-neutral-500);
  text-align: right;
}
.clear-btn {
  margin-top: var(--space-2);
  font-size: 11px;
  padding: 4px 10px;
  align-self: flex-start;
}
</style>
