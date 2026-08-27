// maplibre CSS'i burada: bileşen içinde import edilirse Vite onu worker
// parçasına koyuyor ve tarayıcı hiç yüklemiyor — harita konumlanamıyor.
import 'maplibre-gl/dist/maplibre-gl.css'
import { createPinia } from 'pinia'
import { createApp } from 'vue'
import App from '@/App.vue'
import { router } from '@/router'
import '@/style.css'

createApp(App).use(createPinia()).use(router).mount('#app')
