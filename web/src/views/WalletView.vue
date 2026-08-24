<script setup lang="ts">
import QrcodeVue from 'qrcode.vue'
import { computed, onMounted, ref } from 'vue'
import { apiFetch, errorMessage } from '@/api/client'
import type { TicketListItemDto } from '@/types'

const tickets = ref<TicketListItemDto[]>([])
const error = ref<string | null>(null)
const loading = ref(true)
const openTicketId = ref<string | null>(null)

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })
const date = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })

function isUsable(ticket: TicketListItemDto): boolean {
  return ticket.status === 'Issued' && new Date(ticket.expiresAt) > new Date()
}

// Süresi geçmiş ama hâlâ Issued görünen biletler de geçmişe düşer:
// süre dolumunu işaretleyen bir arka plan işi yok.
const activeTickets = computed(() => tickets.value.filter(isUsable))
const pastTickets = computed(() => tickets.value.filter((t) => !isUsable(t)))

function statusLabel(ticket: TicketListItemDto): string {
  if (ticket.status === 'Redeemed') return 'Kullanıldı'
  if (ticket.status === 'Cancelled') return 'İptal edildi'

  return 'Süresi doldu'
}

function toggle(id: string) {
  openTicketId.value = openTicketId.value === id ? null : id
}

onMounted(async () => {
  try {
    tickets.value = await apiFetch<TicketListItemDto[]>('/api/tickets')
  } catch (err) {
    error.value = errorMessage(err, 'Biletler yüklenemedi.')
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <main class="min-h-screen bg-slate-50">
    <header class="flex items-center gap-3 border-b border-slate-200 bg-white px-4 py-3">
      <RouterLink :to="{ name: 'stations' }" class="text-sm text-slate-500 hover:text-slate-900">
        ← Geri
      </RouterLink>
      <h1 class="text-lg font-semibold text-slate-900">Biletlerim</h1>
    </header>

    <div class="mx-auto max-w-2xl space-y-8 p-4">
      <p v-if="loading" class="py-8 text-center text-sm text-slate-500">Yükleniyor…</p>

      <p
        v-else-if="error"
        class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
        role="alert"
      >
        {{ error }}
      </p>

      <p v-else-if="tickets.length === 0" class="py-8 text-center text-sm text-slate-500">
        Henüz biletiniz yok.
      </p>

      <template v-else>
        <section>
          <h2 class="mb-2 text-sm font-medium text-slate-700">Aktif biletler</h2>

          <p v-if="activeTickets.length === 0" class="text-sm text-slate-500">
            Kullanılabilir biletiniz yok.
          </p>

          <ul v-else class="space-y-2">
            <li
              v-for="ticket in activeTickets"
              :key="ticket.id"
              class="overflow-hidden rounded-xl bg-white ring-1 ring-slate-200"
            >
              <button
                type="button"
                class="flex w-full items-start justify-between gap-4 p-4 text-left"
                :aria-expanded="openTicketId === ticket.id"
                @click="toggle(ticket.id)"
              >
                <span class="min-w-0">
                  <span class="block font-medium text-slate-900">{{ ticket.serviceName }}</span>
                  <span class="mt-0.5 block text-sm text-slate-500">{{ ticket.stationName }}</span>
                  <span class="mt-1 block text-xs text-slate-400">
                    Son kullanım: {{ date.format(new Date(ticket.expiresAt)) }}
                  </span>
                </span>
                <span class="shrink-0 text-sm font-semibold text-slate-900">
                  {{ money.format(ticket.amount) }}
                </span>
              </button>

              <div
                v-if="openTicketId === ticket.id"
                class="flex flex-col items-center gap-3 border-t border-slate-100 bg-slate-50 p-6"
              >
                <QrcodeVue :value="ticket.code" :size="220" level="Q" render-as="svg" />
                <p class="text-center text-xs text-slate-500">
                  Bu kodu yıkama noktasındaki personele okutun.
                </p>
              </div>
            </li>
          </ul>
        </section>

        <section v-if="pastTickets.length > 0">
          <h2 class="mb-2 text-sm font-medium text-slate-700">Geçmiş</h2>

          <ul class="space-y-2">
            <li
              v-for="ticket in pastTickets"
              :key="ticket.id"
              class="flex items-start justify-between gap-4 rounded-xl bg-white p-4 ring-1 ring-slate-200 opacity-60"
            >
              <span class="min-w-0">
                <span class="block font-medium text-slate-900">{{ ticket.serviceName }}</span>
                <span class="mt-0.5 block text-sm text-slate-500">{{ ticket.stationName }}</span>
              </span>
              <span class="shrink-0 text-xs text-slate-500">{{ statusLabel(ticket) }}</span>
            </li>
          </ul>
        </section>
      </template>
    </div>
  </main>
</template>
