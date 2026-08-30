<template>
  <div class="brand-bar">
    <img src="/can.svg" alt="" class="brand-icon">
    <span class="brand-monber">Monber</span>
    <span class="brand-api">API</span>
    <span class="brand-tag">energy-drink can prices<br>nationwide</span>
  </div>
</template>

<style scoped>
.brand-bar {
  grid-column: 1 / 2;
  grid-row: 1;
  pointer-events: auto;
  display: flex;
  align-items: center;
  gap: var(--space-4);
  padding: var(--space-4) var(--space-6);
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-neutral-800);
  background: color-mix(in srgb, var(--color-bg) 82%, transparent);
  backdrop-filter: blur(14px);
  box-shadow: var(--shadow-md);
}
.brand-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}
.brand-monber {
  font-family: var(--font-heading);
  font-weight: 600;
  font-size: 17px;
  letter-spacing: -0.01em;
}
.brand-api {
  font-family: var(--font-heading);
  font-size: 17px;
  color: var(--color-neutral-600);
}
.brand-tag {
  margin-left: auto;
  font-size: 11px;
  color: var(--color-neutral-500);
  text-align: right;
  line-height: 1.3;
}
</style>
