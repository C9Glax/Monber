<script setup lang="ts">
import type { MergedStore } from '../composables/useStorePrices'
import { eur } from '../composables/useStorePrices'

const props = defineProps<{
  current: MergedStore | null
  future: MergedStore | null
}>()

const emit = defineEmits<{ show: [] }>()

const identity = computed(() => props.current ?? props.future)

const startsLabel = computed(() => {
  if (!props.future?.effectiveFrom) return ''
  const date = new Date(props.future.effectiveFrom)
  return `Starts ${date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}`
})
</script>

<template>
  <div v-if="identity" class="card">
    <div class="kicker">Selected store</div>
    <div class="store-row">
      <a v-if="identity.sourceUrl" class="store-name store-link" :href="identity.sourceUrl" target="_blank" rel="noopener noreferrer">{{ identity.brand }}</a>
      <span v-else class="store-name">{{ identity.brand }}</span>
      <span class="store-meta">{{ identity.dist.toFixed(1) }} km<template v-if="identity.name"> · {{ identity.name }}</template></span>
    </div>

    <div class="price-block">
      <div class="price-block-kicker">Now</div>
      <div class="price-row">
        <div class="price">{{ eur(current?.perCanPrice) }}</div>
        <div class="price-meta">
          <div class="per-can">per 500&nbsp;ml can</div>
          <div class="pack">{{ current?.pack ?? '' }}</div>
        </div>
      </div>
    </div>

    <div v-if="future?.perCanPrice != null" class="price-block">
      <div class="kicker-row">
        <div class="price-block-kicker">Soon</div>
        <span class="tag tag-accent">{{ startsLabel }}</span>
      </div>
      <div class="price-row">
        <div class="price">{{ eur(future.perCanPrice) }}</div>
        <div class="price-meta">
          <div class="per-can">per 500&nbsp;ml can</div>
          <div class="pack">{{ future.pack ?? '' }}</div>
        </div>
      </div>
    </div>

    <button class="btn btn-primary btn-block" :disabled="!identity" @click="emit('show')">
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
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
.kicker {
  font-size: 11px;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: var(--color-accent-400);
}
.store-row {
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
.price-block {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
.kicker-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}
.kicker-row .tag {
  font-size: 10px;
  flex: none;
}
.price-block-kicker {
  font-size: 10px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-neutral-600);
}
.price-row {
  display: flex;
  align-items: flex-start;
  gap: var(--space-4);
}
.price {
  font-family: var(--font-heading);
  font-weight: 500;
  font-size: 44px;
  line-height: 0.85;
  letter-spacing: -0.04em;
}
.price-meta {
  padding-top: 2px;
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
</style>
