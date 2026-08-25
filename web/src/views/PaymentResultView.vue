<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import type { OrderStatus, OrderStatusResponse } from '@/types'

const route = useRoute()

const orderId = computed(() => (typeof route.query.orderId === 'string' ? route.query.orderId : ''))
const callbackFailed = computed(() => route.query.status === 'failed')

type Phase = 'pending' | 'success' | 'failed' | 'timeout'

const phase = ref<Phase>('pending')
const order = ref<OrderStatusResponse | null>(null)
const message = ref<string | null>(null)
const attempts = ref(0)

// Bileti webhook/callback kesinleştiriyor; birkaç saniye gecikebilir.
const POLL_INTERVAL_MS = 1500
const MAX_ATTEMPTS = 10

const SUCCESS_STATES: readonly OrderStatus[] = ['Paid', 'Redeemed', 'Settled']
const FAILURE_STATES: readonly OrderStatus[] = ['Failed', 'Expired', 'Refunded']

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })

let timer: ReturnType<typeof setTimeout> | undefined
let stopped = false

async function poll(): Promise<void> {
  if (stopped) {
    return
  }

  attempts.value += 1

  try {
    const result = await apiFetch<OrderStatusResponse>(`/api/orders/${orderId.value}`)
    order.value = result

    if (SUCCESS_STATES.includes(result.status)) {
      phase.value = 'success'
      return
    }

    if (FAILURE_STATES.includes(result.status)) {
      phase.value = 'failed'
      message.value = 'Ödeme tamamlanamadı.'
      return
    }
  } catch (err) {
    // Geçici hata olabilir; üst sınıra kadar denemeye devam.
    message.value = errorMessage(err, 'Sipariş durumu alınamadı.')
  }

  if (attempts.value >= MAX_ATTEMPTS) {
    phase.value = 'timeout'
    return
  }

  timer = setTimeout(poll, POLL_INTERVAL_MS)
}

function retry() {
  attempts.value = 0
  message.value = null
  phase.value = 'pending'
  void poll()
}

onMounted(() => {
  if (!orderId.value) {
    phase.value = 'failed'
    message.value = 'Sipariş bilgisi bulunamadı.'
    return
  }

  if (callbackFailed.value) {
    phase.value = 'failed'
    message.value = 'Ödeme reddedildi.'
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
  <main class="flex min-h-dvh items-center justify-center bg-brand-mist p-4">
    <div class="w-full max-w-sm overflow-hidden rounded-2xl bg-white shadow-xl">
      <!-- Durum başlığı -->
      <div
        class="px-6 py-8 text-center text-white"
        :class="{
          'bg-brand-green': phase === 'success',
          'bg-red-600': phase === 'failed',
          'bg-brand-navy': phase === 'pending' || phase === 'timeout',
        }"
      >
        <div
          v-if="phase === 'pending'"
          class="mx-auto h-10 w-10 animate-spin rounded-full border-4 border-white/30 border-t-white"
          aria-hidden="true"
        />
        <p v-else class="text-5xl" aria-hidden="true">
          {{ phase === 'success' ? '✓' : phase === 'failed' ? '✕' : '⏳' }}
        </p>

        <h1 class="mt-3 text-lg font-bold">
          {{
            phase === 'success'
              ? 'Ödeme Onaylandı'
              : phase === 'failed'
                ? 'Ödeme Başarısız'
                : phase === 'timeout'
                  ? 'Bir sorun oluştu'
                  : 'Ödeme doğrulanıyor'
          }}
        </h1>
      </div>

      <div class="space-y-4 p-6">
        <!-- Sipariş özeti -->
        <div v-if="order && phase === 'success'" class="space-y-2 rounded-xl bg-brand-mist p-4">
          <p v-if="order.itemSummary" class="text-sm text-slate-600">{{ order.itemSummary }}</p>
          <div class="flex items-center justify-between border-t border-slate-200 pt-2">
            <span class="text-sm text-slate-500">Toplam</span>
            <span class="font-bold text-brand-navy">{{ money.format(order.amount) }}</span>
          </div>
        </div>

        <p v-if="phase === 'pending'" class="text-center text-sm text-slate-500">
          Bu işlem birkaç saniye sürebilir, sayfayı kapatmayın.
        </p>

        <p v-else-if="phase === 'success'" class="text-center text-sm text-slate-500">
          Biletleriniz hazır.
        </p>

        <p v-else-if="phase === 'timeout'" class="text-center text-sm text-slate-500">
          Ödemeniz alınmış olabilir ama henüz onaylanmadı. Paranız çekildiyse bilet
          kısa süre içinde tanımlanır.
        </p>

        <p v-else class="text-center text-sm text-slate-500">
          {{ message ?? 'İşlem tamamlanamadı.' }}
        </p>

        <!-- Eylemler -->
        <RouterLink
          v-if="phase === 'success'"
          :to="{ name: 'wallet' }"
          class="block rounded-lg bg-brand-blue px-4 py-3 text-center font-semibold text-white transition hover:bg-brand-blue-dark"
        >
          Biletlerim
        </RouterLink>

        <button
          v-if="phase === 'timeout'"
          type="button"
          class="w-full rounded-lg bg-brand-blue px-4 py-3 font-semibold text-white transition hover:bg-brand-blue-dark"
          @click="retry"
        >
          Tekrar Dene
        </button>

        <RouterLink
          v-if="phase === 'failed'"
          :to="{ name: 'stations' }"
          class="block rounded-lg bg-brand-blue px-4 py-3 text-center font-semibold text-white transition hover:bg-brand-blue-dark"
        >
          Tekrar Dene
        </RouterLink>

        <RouterLink
          :to="{ name: 'stations' }"
          class="block rounded-lg px-4 py-2.5 text-center text-sm font-medium text-slate-600 ring-1 ring-slate-300 transition hover:bg-slate-50"
        >
          Yıkama noktalarına dön
        </RouterLink>
      </div>
    </div>
  </main>
</template>
