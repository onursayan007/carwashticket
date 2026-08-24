<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch, errorMessage } from '@/api/client'
import type { CreateOrderResponse, StationDetailDto } from '@/types'

const props = defineProps<{ id: string }>()

const station = ref<StationDetailDto | null>(null)
const selectedServiceId = ref<string | null>(null)
const error = ref<string | null>(null)
const loading = ref(true)

const checkoutError = ref<string | null>(null)
const submitting = ref(false)

// Aynı seçim için sabit kalır: istek başarısız olup tekrar denenirse sunucu
// bunu aynı sipariş olarak tanır. Seçim değişince yenilenir.
const idempotencyKey = ref(crypto.randomUUID())

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })

const selectedService = computed(
  () => station.value?.services.find((s) => s.id === selectedServiceId.value) ?? null,
)

onMounted(async () => {
  try {
    station.value = await apiFetch<StationDetailDto>(`/api/stations/${props.id}`)
  } catch (err) {
    error.value = errorMessage(err, 'İstasyon bilgisi yüklenemedi.')
  } finally {
    loading.value = false
  }
})

watch(selectedServiceId, () => {
  idempotencyKey.value = crypto.randomUUID()
  checkoutError.value = null
})

async function startCheckout() {
  if (!station.value || !selectedService.value || submitting.value) {
    return
  }

  submitting.value = true
  checkoutError.value = null

  try {
    const order = await apiFetch<CreateOrderResponse>('/api/orders', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey.value },
      body: { stationId: station.value.id, serviceId: selectedService.value.id },
    })

    if (!order.redirectUrl) {
      checkoutError.value = 'Ödeme adresi alınamadı.'
      return
    }

    // SPA'dan çıkıp sağlayıcının 3DS ekranına gidiyoruz.
    window.location.href = order.redirectUrl
  } catch (err) {
    checkoutError.value = errorMessage(err, 'Sipariş oluşturulamadı.')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="min-h-screen bg-slate-50 pb-24">
    <header class="flex items-center gap-3 border-b border-slate-200 bg-white px-4 py-3">
      <RouterLink :to="{ name: 'stations' }" class="text-sm text-slate-500 hover:text-slate-900">
        ← Geri
      </RouterLink>
      <h1 class="truncate text-lg font-semibold text-slate-900">
        {{ station?.name ?? 'İstasyon' }}
      </h1>
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

      <template v-else-if="station">
        <p v-if="station.address" class="text-sm text-slate-500">{{ station.address }}</p>

        <h2 class="mt-6 mb-2 text-sm font-medium text-slate-700">Hizmetler</h2>

        <p v-if="station.services.length === 0" class="text-sm text-slate-500">
          Bu istasyonda tanımlı hizmet yok.
        </p>

        <ul v-else class="space-y-2">
          <li v-for="service in station.services" :key="service.id">
            <button
              type="button"
              class="flex w-full items-start justify-between gap-4 rounded-xl bg-white p-4 text-left ring-1 transition"
              :class="
                selectedServiceId === service.id
                  ? 'ring-2 ring-slate-900'
                  : 'ring-slate-200 hover:ring-slate-400'
              "
              @click="selectedServiceId = service.id"
            >
              <span class="min-w-0">
                <span class="block font-medium text-slate-900">{{ service.name }}</span>
                <span v-if="service.description" class="mt-0.5 block text-sm text-slate-500">
                  {{ service.description }}
                </span>
                <span class="mt-1 block text-xs text-slate-400">
                  ~{{ service.durationMinutes }} dakika
                </span>
              </span>
              <span class="shrink-0 font-semibold text-slate-900">
                {{ money.format(service.price) }}
              </span>
            </button>
          </li>
        </ul>
      </template>
    </div>

    <div
      v-if="selectedService"
      class="fixed inset-x-0 bottom-0 border-t border-slate-200 bg-white p-4"
    >
      <div class="mx-auto max-w-2xl space-y-3">
        <p
          v-if="checkoutError"
          class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
          role="alert"
        >
          {{ checkoutError }}
        </p>

        <div class="flex items-center justify-between gap-4">
          <span class="min-w-0 text-sm text-slate-600">
            <span class="block truncate font-medium text-slate-900">
              {{ selectedService.name }}
            </span>
            {{ money.format(selectedService.price) }}
          </span>
          <button
            type="button"
            :disabled="submitting"
            class="shrink-0 rounded-lg bg-slate-900 px-5 py-2 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            @click="startCheckout"
          >
            {{ submitting ? 'Yönlendiriliyor…' : 'Devam et' }}
          </button>
        </div>
      </div>
    </div>
  </main>
</template>
