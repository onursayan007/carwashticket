<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import StationMap from '@/components/StationMap.vue'
import { useGeolocation } from '@/composables/useGeolocation'
import { useAuthStore } from '@/stores/auth'
import type { StationSort, StationSummaryDto } from '@/types'

const auth = useAuthStore()
const router = useRouter()
const { latitude, longitude, state: locationState, locate } = useGeolocation()

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })

const SORTS: { value: StationSort; label: string }[] = [
  { value: 'Best', label: 'En iyi seçim' },
  { value: 'Nearest', label: 'En yakın' },
  { value: 'Cheapest', label: 'En ucuz' },
  { value: 'TopRated', label: 'En yüksek puan' },
]

const sort = ref<StationSort>('Best')
const stations = ref<StationSummaryDto[]>([])
const selectedId = ref<string | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

// Alt sayfa iki yükseklikte: harita görünsün diye kısa, liste için tam.
const expanded = ref(false)

const mapRef = ref<InstanceType<typeof StationMap> | null>(null)

const selectedStation = computed(
  () => stations.value.find((s) => s.id === selectedId.value) ?? null,
)

const heading = computed(() =>
  loading.value ? 'Aranıyor…' : `${stations.value.length} yıkama noktası`,
)

async function load() {
  loading.value = true
  error.value = null

  try {
    const query = new URLSearchParams({
      lat: String(latitude.value),
      lng: String(longitude.value),
      sort: sort.value,
    })

    stations.value = await apiFetch<StationSummaryDto[]>(`/api/stations?${query}`)
  } catch (err) {
    error.value = errorMessage(err, 'Yıkama noktaları yüklenemedi.')
  } finally {
    loading.value = false
  }
}

// Haritadaki pin'e dokunmak seçer ve kartı öne çıkarır; listeye dokunmak doğrudan açar.
function selectStation(id: string) {
  selectedId.value = id
  expanded.value = false

  const station = stations.value.find((s) => s.id === id)

  if (station) {
    mapRef.value?.focus(station)
  }
}

function open(id: string) {
  router.push({ name: 'station-detail', params: { id } })
}

function typeLabel(type: StationSummaryDto['type']): string {
  return type === 'SelfService' ? 'Self servis' : type === 'FullService' ? 'Tam hizmet' : 'Karma'
}

function distanceLabel(km: number | null): string | null {
  if (km === null) return null

  return km < 1 ? `${Math.round(km * 1000)} m` : `${km.toFixed(1)} km`
}

onMounted(async () => {
  await locate()
  await load()
})

watch(sort, load)

function onLogout() {
  auth.logout()
  router.replace({ name: 'login' })
}
</script>

<template>
  <main class="relative h-dvh overflow-hidden bg-slate-100">
    <!-- Harita tüm ekranı kaplıyor, arayüz üstünde yüzüyor. -->
    <div class="absolute inset-0">
      <StationMap
        ref="mapRef"
        :stations="stations"
        :latitude="latitude"
        :longitude="longitude"
        :selected-id="selectedId"
        @select="selectStation"
      />
    </div>

    <!-- Üst çubuk -->
    <div class="pointer-events-none absolute inset-x-0 top-0 z-10 space-y-2.5 p-3">
      <div class="pointer-events-auto flex items-center gap-2">
        <div
          class="flex flex-1 items-center gap-3 rounded-full bg-white px-4 py-2.5 shadow-lg ring-1 ring-black/5"
        >
          <span class="text-base text-slate-400" aria-hidden="true">🔍</span>
          <span class="min-w-0">
            <span class="block text-sm font-semibold text-slate-900">Araç yıkama</span>
            <span class="block truncate text-xs text-slate-500">
              {{ locationState === 'granted' ? 'Konumunuza yakın' : 'Antalya çevresi' }}
            </span>
          </span>
        </div>

        <RouterLink
          :to="{ name: 'wallet' }"
          class="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-white text-lg shadow-lg ring-1 ring-black/5"
          aria-label="Biletlerim"
        >
          🎟️
        </RouterLink>

        <button
          type="button"
          class="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-white text-lg shadow-lg ring-1 ring-black/5"
          aria-label="Çıkış"
          @click="onLogout"
        >
          ⏻
        </button>
      </div>

      <!-- Sıralama çipleri -->
      <div class="pointer-events-auto -mx-3 flex gap-2 overflow-x-auto px-3 pb-1">
        <button
          v-for="option in SORTS"
          :key="option.value"
          type="button"
          class="shrink-0 rounded-full px-3.5 py-1.5 text-sm font-medium shadow-md ring-1 transition"
          :class="
            sort === option.value
              ? 'bg-slate-900 text-white ring-slate-900'
              : 'bg-white text-slate-700 ring-black/5 hover:bg-slate-50'
          "
          @click="sort = option.value"
        >
          {{ option.label }}
        </button>
      </div>
    </div>

    <!-- Pin'e dokununca çıkan yüzen kart -->
    <Transition
      enter-active-class="transition duration-200"
      enter-from-class="translate-y-4 opacity-0"
      leave-active-class="transition duration-150"
      leave-to-class="translate-y-4 opacity-0"
    >
      <div
        v-if="selectedStation && !expanded"
        class="absolute inset-x-3 bottom-[19rem] z-30"
      >
        <div class="relative overflow-hidden rounded-2xl bg-white shadow-2xl ring-1 ring-black/5">
          <button
            type="button"
            class="absolute top-2 right-2 grid h-7 w-7 place-items-center rounded-full bg-white/90 text-slate-500 shadow ring-1 ring-black/5"
            aria-label="Kapat"
            @click="selectedId = null"
          >
            ×
          </button>

          <button
            type="button"
            class="w-full p-4 pr-10 text-left"
            @click="open(selectedStation.id)"
          >
            <p class="font-semibold text-slate-900">{{ selectedStation.name }}</p>
            <p class="mt-0.5 text-sm text-slate-500">
              {{ typeLabel(selectedStation.type) }}
              <template v-if="distanceLabel(selectedStation.distanceKm)">
                · {{ distanceLabel(selectedStation.distanceKm) }}
              </template>
            </p>

            <div class="mt-3 flex items-center justify-between">
              <span class="text-sm font-medium text-amber-600">
                ★ {{ selectedStation.ratingAverage.toFixed(1) }}
                <span class="font-normal text-slate-400">({{ selectedStation.ratingCount }})</span>
              </span>
              <span v-if="selectedStation.minPrice !== null" class="font-semibold text-slate-900">
                {{ money.format(selectedStation.minPrice) }}<span
                  class="text-sm font-normal text-slate-400"
                >'den</span>
              </span>
            </div>
          </button>
        </div>
      </div>
    </Transition>

    <!-- Alt sayfa -->
    <section
      class="absolute inset-x-0 bottom-0 z-20 flex flex-col rounded-t-3xl bg-white shadow-2xl transition-[height] duration-300"
      :class="expanded ? 'h-[82%]' : 'h-72'"
    >
      <button
        type="button"
        class="flex w-full flex-col items-center gap-2 pt-2.5 pb-1"
        :aria-expanded="expanded"
        @click="expanded = !expanded"
      >
        <span class="h-1.5 w-10 rounded-full bg-slate-300" aria-hidden="true" />
        <span class="flex w-full items-center justify-between px-4">
          <span class="text-sm font-semibold text-slate-900">{{ heading }}</span>
          <span class="text-xs font-medium text-slate-500">
            {{ expanded ? 'Haritayı gör' : 'Listeyi aç' }}
          </span>
        </span>
      </button>

      <div class="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
        <p v-if="error" class="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
          {{ error }}
        </p>

        <p v-else-if="loading" class="py-8 text-center text-sm text-slate-500">Yükleniyor…</p>

        <p v-else-if="stations.length === 0" class="py-8 text-center text-sm text-slate-500">
          Yakınınızda yıkama noktası bulunamadı.
        </p>

        <ul v-else class="space-y-2">
          <li v-for="station in stations" :key="station.id">
            <button
              type="button"
              class="w-full rounded-2xl border p-3 text-left transition"
              :class="
                selectedId === station.id
                  ? 'border-slate-900 bg-slate-50'
                  : 'border-slate-200 hover:border-slate-300'
              "
              @click="open(station.id)"
            >
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <p class="truncate font-semibold text-slate-900">{{ station.name }}</p>
                  <p class="mt-0.5 truncate text-xs text-slate-500">
                    {{ station.district }}<template v-if="station.city">, {{ station.city }}</template>
                  </p>
                </div>

                <span
                  class="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600"
                >
                  {{ typeLabel(station.type) }}
                </span>
              </div>

              <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                <span class="font-medium text-amber-600">
                  ★ {{ station.ratingAverage.toFixed(1) }}
                  <span class="font-normal text-slate-400">({{ station.ratingCount }})</span>
                </span>

                <span v-if="distanceLabel(station.distanceKm)" class="text-slate-500">
                  {{ distanceLabel(station.distanceKm) }}
                </span>

                <span v-if="station.minPrice !== null" class="ml-auto font-semibold text-slate-900">
                  {{ money.format(station.minPrice) }}<span class="font-normal text-slate-400">'den</span>
                </span>
              </div>
            </button>
          </li>
        </ul>
      </div>
    </section>
  </main>
</template>
