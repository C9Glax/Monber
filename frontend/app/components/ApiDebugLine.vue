<script setup lang="ts">
defineProps<{
  poiUrl: string
  pricesUrl: string
}>()

const toastVisible = ref(false)
let toastTimeout: ReturnType<typeof setTimeout> | undefined

async function copy(url: string) {
  const fullUrl = new URL(url, window.location.origin).toString()
  try {
    await navigator.clipboard.writeText(fullUrl)
  }
  catch {
    return
  }
  toastVisible.value = true
  clearTimeout(toastTimeout)
  toastTimeout = setTimeout(() => {
    toastVisible.value = false
  }, 2000)
}
</script>

<template>
  <div class="debug-line">
    <button type="button" class="debug-link" @click="copy(poiUrl)">GET {{ poiUrl }}</button>
    <button type="button" class="debug-link" @click="copy(pricesUrl)">GET {{ pricesUrl }}</button>
  </div>

  <Teleport to="body">
    <Transition name="toast-fade">
      <div v-if="toastVisible" class="copy-toast">Copied to clipboard</div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.debug-line {
  flex: none;
  display: flex;
  flex-direction: column;
  padding: var(--space-3) var(--space-4);
  font-size: 10px;
  color: var(--color-neutral-700);
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}
.debug-link {
  all: unset;
  cursor: pointer;
  white-space: pre-wrap;
  word-break: break-all;
}
.debug-link:hover,
.debug-link:focus-visible {
  color: var(--color-accent-300);
  text-decoration: underline;
}
</style>

<style>
.copy-toast {
  position: fixed;
  left: var(--space-6);
  bottom: var(--space-6);
  z-index: 2000;
  padding: var(--space-3) var(--space-5);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 86%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
  font-size: 13px;
  color: var(--color-neutral-200);
}
.toast-fade-enter-active,
.toast-fade-leave-active {
  transition: opacity 0.2s ease;
}
.toast-fade-enter-from,
.toast-fade-leave-to {
  opacity: 0;
}
</style>
