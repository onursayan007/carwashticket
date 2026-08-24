import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // maplibre-gl worker'ını `new URL('./maplibre-gl-worker.mjs', import.meta.url)`
  // ile yüklüyor. Vite ön-paketlerse import.meta.url .vite/deps/ altını gösteriyor,
  // worker dosyası orada olmadığı için 404 alıyor ve tile'lar ayrıştırılamıyor:
  // harita zemini çizilir ama üstünde hiçbir veri görünmez.
  optimizeDeps: {
    exclude: ['maplibre-gl'],
  },
  server: {
    port: 5173,
  },
})
