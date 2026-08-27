<script setup lang="ts">
import { Map as MapLibreMap, Marker, NavigationControl, setWorkerUrl } from 'maplibre-gl'
// maplibre worker'ının adresini kendi paket chunk'ından türetiyor; paketlendikten
// sonra o adres yanlış oluyor ve worker 404 alıyor. Worker açılmayınca tile'lar
// ayrıştırılamıyor: zemin çizilir, üstünde hiçbir veri görünmez.
// Vite'ın ?url eki dosyayı asset olarak yayınlayıp doğru adresi veriyor.
import maplibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'
import { onMounted, onUnmounted, ref, watch } from 'vue'
import type { StationSummaryDto } from '@/types'

const props = defineProps<{
  stations: StationSummaryDto[]
  latitude: number
  longitude: number
  selectedId: string | null
}>()

const emit = defineEmits<{ select: [id: string] }>()

setWorkerUrl(maplibreWorkerUrl)

const container = ref<HTMLDivElement | null>(null)
const mapError = ref<string | null>(null)

// Geçici teşhis: harita boş kalırsa nerede takıldığını ekrandan okuyalım.
const diag = ref({ style: '—', kaynak: 0, yuklendi: 0, hata: 0, son: '', boyut: '' })

let map: MapLibreMap | null = null
let resizeObserver: ResizeObserver | null = null
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

  // Self serviste birim satılıyor (su/köpük) — camgöbeği; tam hizmet lacivert.
  const selfService = station.type !== 'FullService'

  el.className = [
    'flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-bold shadow-lg',
    'ring-2 transition-transform',
    selected
      ? 'bg-brand-navy text-white ring-white scale-110'
      : selfService
        ? 'bg-white text-brand-water ring-brand-water/40'
        : 'bg-white text-brand-navy ring-brand-navy/20',
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
  el.className = 'h-4 w-4 rounded-full bg-brand-blue ring-4 ring-brand-blue/30'

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

  instance.on('styledata', () => {
    diag.value.style = 'OK'
  })

  instance.on('dataloading', (event) => {
    if (event.dataType === 'source') diag.value.kaynak += 1
  })

  instance.on('data', (event) => {
    if (event.dataType === 'source' && event.isSourceLoaded) diag.value.yuklendi += 1
  })

  setInterval(() => {
    const c = container.value
    diag.value.boyut = c ? `${c.clientWidth}x${c.clientHeight}` : 'yok'
  }, 1000)

  // Tile veya stil hatası sessizce boş harita bırakmasın.
  instance.on('error', (event) => {
    const error = event.error as { status?: number; message?: string } | undefined

    mapError.value = error?.status === 403 || error?.status === 401
      ? 'Harita anahtarı reddedildi (403). MapTiler anahtarını kontrol edin.'
      : 'Harita katmanı yüklenemedi.'

    diag.value.hata += 1
    diag.value.son = `${error?.status ?? ''} ${error?.message ?? ''}`.trim().slice(0, 70)

    console.error('[harita]', event.error)
  })


  // Kapsayıcı sonradan boyutlanırsa (alt sayfa animasyonu vb.) canvas'ı güncelle.
  resizeObserver = new ResizeObserver(() => instance.resize())
  resizeObserver.observe(container.value)
})

onUnmounted(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
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
  <div class="relative h-full w-full">
    <div ref="container" class="h-full w-full" />

    <p
      class="absolute top-1/2 left-1/2 z-50 -translate-x-1/2 -translate-y-1/2 rounded-lg bg-black/85 px-3 py-2 text-center font-mono text-xs leading-relaxed text-white shadow-2xl"
    >
      stil:{{ diag.style }} kaynak:{{ diag.kaynak }} yuklendi:{{ diag.yuklendi }}
      hata:{{ diag.hata }} boyut:{{ diag.boyut }}
      <template v-if="diag.son"><br />{{ diag.son }}</template>
    </p>

    <p
      v-if="mapError"
      class="absolute inset-x-3 top-3 z-10 rounded-lg bg-red-600 px-3 py-2 text-xs font-medium text-white shadow-lg"
      role="alert"
    >
      {{ mapError }}
    </p>
  </div>
</template>
