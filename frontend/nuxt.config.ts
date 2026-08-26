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

  runtimeConfig: {
    public: {
      apiBase: 'http://localhost:5090',
    },
  },

  devServer: {
    port: 3000,
  },
})
