<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import { useAuthStore } from '@/stores/auth'
import type { StationListItemDto } from '@/types'

const auth = useAuthStore()
const router = useRouter()

const stations = ref<StationListItemDto[]>([])
const error = ref<string | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    stations.value = await apiFetch<StationListItemDto[]>('/api/stations')
  } catch (err) {
    error.value = errorMessage(err, 'İstasyonlar yüklenemedi.')
  } finally {
    loading.value = false
  }
})

function onLogout() {
  auth.logout()
  router.replace({ name: 'login' })
}
</script>

<template>
  <main class="min-h-screen bg-slate-50">
    <header class="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3">
      <h1 class="text-lg font-semibold text-slate-900">İstasyonlar</h1>
      <button
        type="button"
        class="text-sm text-slate-500 hover:text-slate-900"
        @click="onLogout"
      >
        Çıkış
      </button>
    </header>

    <div class="mx-auto max-w-2xl p-4">
      <p v-if="loading" class="py-8 text-center text-sm text-slate-500">Yükleniyor…</p>

      <p
        v-else-if="error"
        class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
        role="alert"
      >
        {{ error }}
      </p>

      <p v-else-if="stations.length === 0" class="py-8 text-center text-sm text-slate-500">
        Şu anda hizmet veren istasyon yok.
      </p>

      <ul v-else class="space-y-2">
        <li v-for="station in stations" :key="station.id">
          <RouterLink
            :to="{ name: 'station-detail', params: { id: station.id } }"
            class="block rounded-xl bg-white p-4 ring-1 ring-slate-200 hover:ring-slate-400"
          >
            <p class="font-medium text-slate-900">{{ station.name }}</p>
            <p v-if="station.address" class="mt-0.5 text-sm text-slate-500">
              {{ station.address }}
            </p>
          </RouterLink>
        </li>
      </ul>
    </div>
  </main>
</template>
