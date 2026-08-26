<script setup lang="ts">
defineProps<{
  poiUrl: string
  pricesUrl: string
}>()
</script>

<template>
  <div class="debug-line">
    <div>GET {{ poiUrl }}</div>
    <div>GET {{ pricesUrl }}</div>
  </div>
</template>

<style scoped>
.debug-line {
  flex: none;
  padding: var(--space-3) var(--space-4);
  font-size: 10px;
  color: var(--color-neutral-700);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}
</style>
