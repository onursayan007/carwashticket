<script setup lang="ts">
import { Map as MapLibreMap, Marker, NavigationControl, setWorkerUrl } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?url'
import { onMounted, onUnmounted, ref, watch } from 'vue'

const props = defineProps<{ latitude: number; longitude: number }>()
const emit = defineEmits<{ pick: [lat: number, lng: number] }>()

setWorkerUrl(maplibreWorkerUrl)

const container = ref<HTMLDivElement | null>(null)

let map: MapLibreMap | null = null
let marker: Marker | null = null

const key = import.meta.env.VITE_MAPTILER_KEY

const styleUrl = key
  ? `https://api.maptiler.com/maps/streets-v2/style.json?key=${key}`
  : 'https://demotiles.maplibre.org/style.json'

onMounted(() => {
  if (!container.value) {
    return
  }

  map = new MapLibreMap({
    container: container.value,
    style: styleUrl,
    center: [props.longitude, props.latitude],
    zoom: 13,
  })

  map.addControl(new NavigationControl({ showCompass: false }), 'top-right')

  marker = new Marker({ draggable: true, color: '#0071c2' })
    .setLngLat([props.longitude, props.latitude])
    .addTo(map)

  // Sürükleyerek veya haritaya dokunarak konum seçilebiliyor.
  marker.on('dragend', () => {
    const position = marker!.getLngLat()
    emit('pick', position.lat, position.lng)
  })

  map.on('click', (event) => {
    marker!.setLngLat(event.lngLat)
    emit('pick', event.lngLat.lat, event.lngLat.lng)
  })
})

onUnmounted(() => {
  map?.remove()
  map = null
})

// Koordinat elle yazıldığında pin de takip etsin.
watch(
  () => [props.latitude, props.longitude],
  ([lat, lng]) => {
    marker?.setLngLat([lng, lat])
  },
)
</script>

<template>
  <div ref="container" class="h-64 w-full overflow-hidden rounded-xl ring-1 ring-slate-300" />
</template>
