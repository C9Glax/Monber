// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  ssr: false,

  css: [
    '@phosphor-icons/web/regular/style.css',
    '~/assets/css/main.css',
  ],

  app: {
    head: {
      title: 'Monber',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1' },
      ],
    },
  },

  // Empty by default: the app is served through MonberAPI.Gateway, which also proxies
  // /poi and /prices on the same origin, so relative URLs just work. Override with
  // NUXT_PUBLIC_API_BASE (e.g. http://localhost:5090) only when running the frontend
  // standalone against a gateway on a different origin (see AllowedOrigins CORS config).
  runtimeConfig: {
    public: {
      apiBase: '',
      // MapTiler API key for the basemap tiles (see MapView.client.vue). Set via
      // NUXT_PUBLIC_MAP_TILER_KEY (Nuxt maps camelCase runtimeConfig keys to env vars by
      // splitting on capitals) - injected by Aspire from MonberAPI.AppHost/MAPTILER_API.key
      // when run through the AppHost. Falls back to plain OSM tiles if unset.
      mapTilerKey: '',
    },
  },

  devServer: {
    port: 3000,
  },
})
