<script setup lang="ts">
import { Map as MapLibreMap, Marker, NavigationControl } from 'maplibre-gl'
import 'maplibre-gl/dist/maplibre-gl.css'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import type { StationSummaryDto } from '@/types'

const props = defineProps<{
  stations: StationSummaryDto[]
  latitude: number
  longitude: number
  selectedId: string | null
}>()

const emit = defineEmits<{ select: [id: string] }>()

const container = ref<HTMLDivElement | null>(null)

let map: MapLibreMap | null = null
let markers = new Map<string, Marker>()
let userMarker: Marker | null = null

// MapTiler anahtarı varsa gerçek harita, yoksa MapLibre'nin demo tile'ları.
// Demo tile'lar sadece kaba ülke sınırlarını gösterir; anahtar girilince düzelir.
const key = import.meta.env.VITE_MAPTILER_KEY

const styleUrl = key
  ? `https://api.maptiler.com/maps/streets-v2/style.json?key=${key}`
  : 'https://demotiles.maplibre.org/style.json'

function buildMarkerElement(station: StationSummaryDto, selected: boolean): HTMLElement {
  const el = document.createElement('button')
  el.type = 'button'
  el.className = [
    'flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold shadow-lg',
    'ring-2 transition-transform',
    selected
      ? 'bg-slate-900 text-white ring-white scale-110'
      : 'bg-white text-slate-900 ring-slate-900/10',
  ].join(' ')

  el.textContent = station.minPrice !== null ? `${Math.round(station.minPrice)}₺` : '•'
  el.setAttribute('aria-label', station.name)
  el.addEventListener('click', (event) => {
    event.stopPropagation()
    emit('select', station.id)
  })

  return el
}

function renderMarkers() {
  if (!map) {
    return
  }

  for (const marker of markers.values()) {
    marker.remove()
  }

  markers = new Map()

  for (const station of props.stations) {
    const marker = new Marker({
      element: buildMarkerElement(station, station.id === props.selectedId),
    })
      .setLngLat([station.longitude, station.latitude])
      .addTo(map)

    markers.set(station.id, marker)
  }
}

function renderUser() {
  if (!map) {
    return
  }

  const el = document.createElement('div')
  el.className = 'h-4 w-4 rounded-full bg-blue-600 ring-4 ring-blue-600/30'

  userMarker?.remove()
  userMarker = new Marker({ element: el })
    .setLngLat([props.longitude, props.latitude])
    .addTo(map)
}

onMounted(() => {
  if (!container.value) {
    return
  }

  map = new MapLibreMap({
    container: container.value,
    style: styleUrl,
    center: [props.longitude, props.latitude],
    zoom: 12,
    attributionControl: { compact: true },
  })

  map.addControl(new NavigationControl({ showCompass: false }), 'top-right')

  const instance = map

  instance.on('load', () => {
    renderUser()
    renderMarkers()
  })
})

onUnmounted(() => {
  map?.remove()
  map = null
})

watch(() => props.stations, renderMarkers, { deep: true })
watch(() => props.selectedId, renderMarkers)

watch(
  () => [props.latitude, props.longitude],
  ([lat, lng]) => {
    renderUser()
    map?.easeTo({ center: [lng, lat] })
  },
)

// Listeden bir işyeri seçilince haritayı oraya kaydır.
defineExpose({
  focus(station: StationSummaryDto) {
    map?.easeTo({ center: [station.longitude, station.latitude], zoom: 14 })
  },
})
</script>

<template>
  <div ref="container" class="h-full w-full" />
</template>
