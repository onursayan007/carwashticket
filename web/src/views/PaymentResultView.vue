<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import type { OrderStatus, OrderStatusResponse } from '@/types'

const route = useRoute()

// Backend 3DS dönüşünde buraya orderId ile yönlendiriyor.
const orderId = computed(() => (typeof route.query.orderId === 'string' ? route.query.orderId : ''))
const callbackFailed = computed(() => route.query.status === 'failed')

type Phase = 'pending' | 'success' | 'failed' | 'timeout'

const phase = ref<Phase>('pending')
const message = ref<string | null>(null)
const attempts = ref(0)

// Bilet webhook ile üretiliyor; sağlayıcının bildirimi callback'ten sonra gelir.
// 2 sn aralıkla en fazla 30 deneme = 1 dakika.
const POLL_INTERVAL_MS = 2000
const MAX_ATTEMPTS = 30

const SUCCESS_STATES: readonly OrderStatus[] = ['Paid', 'Redeemed', 'Settled']
const FAILURE_STATES: readonly OrderStatus[] = ['Failed', 'Expired', 'Refunded']

let timer: ReturnType<typeof setTimeout> | undefined
let stopped = false

async function poll(): Promise<void> {
  if (stopped) {
    return
  }

  attempts.value += 1

  try {
    const order = await apiFetch<OrderStatusResponse>(`/api/orders/${orderId.value}`)

    if (SUCCESS_STATES.includes(order.status)) {
      phase.value = 'success'
      return
    }

    if (FAILURE_STATES.includes(order.status)) {
      phase.value = 'failed'
      message.value = 'Ödeme tamamlanamadı.'
      return
    }
  } catch (err) {
    // Geçici bir hata olabilir; üst sınıra kadar denemeye devam ediyoruz.
    message.value = errorMessage(err, 'Sipariş durumu alınamadı.')
  }

  if (attempts.value >= MAX_ATTEMPTS) {
    phase.value = 'timeout'
    return
  }

  timer = setTimeout(poll, POLL_INTERVAL_MS)
}

onMounted(() => {
  if (!orderId.value) {
    phase.value = 'failed'
    message.value = 'Sipariş bilgisi bulunamadı.'
    return
  }

  if (callbackFailed.value) {
    phase.value = 'failed'
    message.value = 'Ödeme sağlayıcısı işlemi reddetti.'
    return
  }

  void poll()
})

onUnmounted(() => {
  stopped = true
  clearTimeout(timer)
})
</script>

<template>
  <main class="flex min-h-screen items-center justify-center bg-slate-50 p-4">
    <div class="w-full max-w-sm space-y-4 rounded-xl bg-white p-6 text-center shadow-sm ring-1 ring-slate-200">
      <template v-if="phase === 'pending'">
        <div
          class="mx-auto h-10 w-10 animate-spin rounded-full border-4 border-slate-200 border-t-slate-900"
          aria-hidden="true"
        />
        <h1 class="text-lg font-semibold text-slate-900">Ödeme doğrulanıyor</h1>
        <p class="text-sm text-slate-500">
          Bu işlem birkaç saniye sürebilir, sayfayı kapatmayın.
        </p>
      </template>

      <template v-else-if="phase === 'success'">
        <p class="text-4xl" aria-hidden="true">✅</p>
        <h1 class="text-lg font-semibold text-slate-900">Ödeme alındı</h1>
        <p class="text-sm text-slate-500">Biletiniz hazır.</p>
      </template>

      <template v-else-if="phase === 'timeout'">
        <p class="text-4xl" aria-hidden="true">⏳</p>
        <h1 class="text-lg font-semibold text-slate-900">Doğrulama sürüyor</h1>
        <p class="text-sm text-slate-500">
          Ödemeniz alınmış olabilir ama henüz onaylanmadı. Paranız çekildiyse bilet
          kısa süre içinde tanımlanır.
        </p>
      </template>

      <template v-else>
        <p class="text-4xl" aria-hidden="true">⚠️</p>
        <h1 class="text-lg font-semibold text-slate-900">Ödeme başarısız</h1>
        <p class="text-sm text-slate-500">{{ message ?? 'İşlem tamamlanamadı.' }}</p>
      </template>

      <RouterLink
        v-if="phase === 'success'"
        :to="{ name: 'wallet' }"
        class="block rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800"
      >
        Biletimi göster
      </RouterLink>

      <RouterLink
        :to="{ name: 'stations' }"
        class="block rounded-lg px-4 py-2 font-medium text-slate-600 ring-1 ring-slate-300 hover:bg-slate-50"
      >
        İstasyonlara dön
      </RouterLink>
    </div>
  </main>
</template>
