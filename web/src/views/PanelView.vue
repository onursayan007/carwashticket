<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import { useAuthStore } from '@/stores/auth'
import type {
  PanelOrderDto,
  PanelServiceDto,
  PanelSummaryDto,
  UpsertServiceRequest,
} from '@/types'

const auth = useAuthStore()
const router = useRouter()

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })
const dateTime = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' })

function isoDay(offsetDays = 0): string {
  const d = new Date()
  d.setDate(d.getDate() + offsetDays)

  return d.toISOString().slice(0, 10)
}

const from = ref(isoDay(-30))
const to = ref(isoDay(0))

const summary = ref<PanelSummaryDto | null>(null)
const orders = ref<PanelOrderDto[]>([])
const services = ref<PanelServiceDto[]>([])

const loading = ref(true)
const error = ref<string | null>(null)

// Hizmet formu: id doluysa düzenleme, boşsa yeni kayıt.
const editingId = ref<string | null>(null)
const form = ref<UpsertServiceRequest>(emptyForm())
const savingService = ref(false)
const serviceError = ref<string | null>(null)

function emptyForm(): UpsertServiceRequest {
  return { name: '', description: null, price: 0, durationMinutes: 15, isActive: true }
}

function rangeQuery(): string {
  // Bitiş gününü de kapsasın diye bir gün ekliyoruz; backend üst sınırı dışlıyor.
  const end = new Date(`${to.value}T00:00:00`)
  end.setDate(end.getDate() + 1)

  return `from=${new Date(`${from.value}T00:00:00`).toISOString()}&to=${end.toISOString()}`
}

async function loadReport() {
  loading.value = true
  error.value = null

  try {
    const query = rangeQuery()

    const [summaryResult, ordersResult] = await Promise.all([
      apiFetch<PanelSummaryDto>(`/api/panel/summary?${query}`),
      apiFetch<PanelOrderDto[]>(`/api/panel/orders?${query}`),
    ])

    summary.value = summaryResult
    orders.value = ordersResult
  } catch (err) {
    error.value = errorMessage(err, 'Rapor yüklenemedi.')
  } finally {
    loading.value = false
  }
}

async function loadServices() {
  try {
    services.value = await apiFetch<PanelServiceDto[]>('/api/panel/services')
  } catch (err) {
    serviceError.value = errorMessage(err, 'Hizmetler yüklenemedi.')
  }
}

function startEdit(service: PanelServiceDto) {
  editingId.value = service.id
  form.value = {
    name: service.name,
    description: service.description,
    price: service.price,
    durationMinutes: service.durationMinutes,
    isActive: service.isActive,
  }
  serviceError.value = null
}

function cancelEdit() {
  editingId.value = null
  form.value = emptyForm()
  serviceError.value = null
}

async function saveService() {
  savingService.value = true
  serviceError.value = null

  try {
    if (editingId.value) {
      await apiFetch<PanelServiceDto>(`/api/panel/services/${editingId.value}`, {
        method: 'PUT',
        body: form.value,
      })
    } else {
      await apiFetch<PanelServiceDto>('/api/panel/services', {
        method: 'POST',
        body: form.value,
      })
    }

    cancelEdit()
    await loadServices()
  } catch (err) {
    serviceError.value = errorMessage(err, 'Hizmet kaydedilemedi.')
  } finally {
    savingService.value = false
  }
}

function onLogout() {
  auth.logout()
  router.replace({ name: 'login' })
}

onMounted(async () => {
  await Promise.all([loadReport(), loadServices()])
})
</script>

<template>
  <main class="min-h-screen bg-slate-50">
    <header class="flex items-center justify-between border-b border-slate-200 bg-white px-4 py-3">
      <h1 class="text-lg font-semibold text-slate-900">Yönetim paneli</h1>
      <button type="button" class="text-sm text-slate-500 hover:text-slate-900" @click="onLogout">
        Çıkış
      </button>
    </header>

    <div class="mx-auto max-w-4xl space-y-8 p-4">
      <p v-if="error" class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
        {{ error }}
      </p>

      <!-- Tarih aralığı -->
      <section class="rounded-xl bg-white p-4 ring-1 ring-slate-200">
        <form class="flex flex-wrap items-end gap-3" @submit.prevent="loadReport">
          <div class="space-y-1">
            <label for="from" class="block text-sm font-medium text-slate-700">Başlangıç</label>
            <input
              id="from"
              v-model="from"
              type="date"
              class="rounded-lg border border-slate-300 px-3 py-2 text-slate-900"
            />
          </div>
          <div class="space-y-1">
            <label for="to" class="block text-sm font-medium text-slate-700">Bitiş</label>
            <input
              id="to"
              v-model="to"
              type="date"
              class="rounded-lg border border-slate-300 px-3 py-2 text-slate-900"
            />
          </div>
          <button
            type="submit"
            :disabled="loading"
            class="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
          >
            {{ loading ? 'Yükleniyor…' : 'Getir' }}
          </button>
        </form>
      </section>

      <!-- Özet -->
      <section v-if="summary" class="grid gap-3 sm:grid-cols-4">
        <div class="rounded-xl bg-white p-4 ring-1 ring-slate-200">
          <p class="text-xs text-slate-500">Sipariş</p>
          <p class="mt-1 text-xl font-semibold text-slate-900">{{ summary.orderCount }}</p>
        </div>
        <div class="rounded-xl bg-white p-4 ring-1 ring-slate-200">
          <p class="text-xs text-slate-500">Ciro</p>
          <p class="mt-1 text-xl font-semibold text-slate-900">
            {{ money.format(summary.grossRevenue) }}
          </p>
        </div>
        <div class="rounded-xl bg-white p-4 ring-1 ring-slate-200">
          <p class="text-xs text-slate-500">Komisyon</p>
          <p class="mt-1 text-xl font-semibold text-slate-500">
            {{ money.format(summary.commission) }}
          </p>
        </div>
        <div class="rounded-xl bg-emerald-50 p-4 ring-1 ring-emerald-200">
          <p class="text-xs text-emerald-700">Hakediş</p>
          <p class="mt-1 text-xl font-semibold text-emerald-900">
            {{ money.format(summary.stationShare) }}
          </p>
        </div>
      </section>

      <!-- Siparişler -->
      <section>
        <h2 class="mb-2 text-sm font-medium text-slate-700">Siparişler</h2>

        <p v-if="orders.length === 0 && !loading" class="text-sm text-slate-500">
          Bu aralıkta sipariş yok.
        </p>

        <div v-else class="overflow-x-auto rounded-xl bg-white ring-1 ring-slate-200">
          <table class="w-full text-left text-sm">
            <thead class="border-b border-slate-200 text-xs text-slate-500">
              <tr>
                <th class="px-4 py-2 font-medium">Tarih</th>
                <th class="px-4 py-2 font-medium">Hizmet</th>
                <th class="px-4 py-2 font-medium">Durum</th>
                <th class="px-4 py-2 text-right font-medium">Tutar</th>
                <th class="px-4 py-2 text-right font-medium">Komisyon</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="order in orders" :key="order.id" class="border-b border-slate-100 last:border-0">
                <td class="whitespace-nowrap px-4 py-2 text-slate-500">
                  {{ dateTime.format(new Date(order.createdAt)) }}
                </td>
                <td class="px-4 py-2 text-slate-900">{{ order.serviceName }}</td>
                <td class="px-4 py-2 text-slate-500">{{ order.status }}</td>
                <td class="whitespace-nowrap px-4 py-2 text-right text-slate-900">
                  {{ money.format(order.amount) }}
                </td>
                <td class="whitespace-nowrap px-4 py-2 text-right text-slate-500">
                  {{ money.format(order.commissionAmount) }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- Hizmetler -->
      <section class="space-y-3">
        <h2 class="text-sm font-medium text-slate-700">Hizmetler</h2>

        <p
          v-if="serviceError"
          class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
          role="alert"
        >
          {{ serviceError }}
        </p>

        <ul class="space-y-2">
          <li
            v-for="service in services"
            :key="service.id"
            class="flex items-center justify-between gap-4 rounded-xl bg-white p-4 ring-1 ring-slate-200"
            :class="{ 'opacity-50': !service.isActive }"
          >
            <span class="min-w-0">
              <span class="block font-medium text-slate-900">{{ service.name }}</span>
              <span class="text-xs text-slate-500">
                {{ service.durationMinutes }} dk
                <template v-if="!service.isActive"> · pasif</template>
              </span>
            </span>
            <span class="flex shrink-0 items-center gap-3">
              <span class="font-semibold text-slate-900">{{ money.format(service.price) }}</span>
              <button
                type="button"
                class="text-sm text-slate-500 hover:text-slate-900"
                @click="startEdit(service)"
              >
                Düzenle
              </button>
            </span>
          </li>
        </ul>

        <form
          class="space-y-3 rounded-xl bg-white p-4 ring-1 ring-slate-200"
          @submit.prevent="saveService"
        >
          <p class="text-sm font-medium text-slate-700">
            {{ editingId ? 'Hizmeti düzenle' : 'Yeni hizmet' }}
          </p>

          <div class="grid gap-3 sm:grid-cols-2">
            <div class="space-y-1">
              <label for="svc-name" class="block text-sm text-slate-600">Ad</label>
              <input
                id="svc-name"
                v-model="form.name"
                type="text"
                required
                maxlength="200"
                class="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
            <div class="space-y-1">
              <label for="svc-price" class="block text-sm text-slate-600">Fiyat (TL)</label>
              <input
                id="svc-price"
                v-model.number="form.price"
                type="number"
                step="0.01"
                min="0.01"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
            <div class="space-y-1">
              <label for="svc-duration" class="block text-sm text-slate-600">Süre (dakika)</label>
              <input
                id="svc-duration"
                v-model.number="form.durationMinutes"
                type="number"
                min="1"
                max="1440"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
            <div class="space-y-1">
              <label for="svc-desc" class="block text-sm text-slate-600">Açıklama</label>
              <input
                id="svc-desc"
                v-model="form.description"
                type="text"
                maxlength="1000"
                class="w-full rounded-lg border border-slate-300 px-3 py-2"
              />
            </div>
          </div>

          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input v-model="form.isActive" type="checkbox" class="rounded border-slate-300" />
            Aktif (müşterilere görünür)
          </label>

          <div class="flex gap-2">
            <button
              type="submit"
              :disabled="savingService"
              class="rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
            >
              {{ savingService ? 'Kaydediliyor…' : 'Kaydet' }}
            </button>
            <button
              v-if="editingId"
              type="button"
              class="rounded-lg px-4 py-2 text-slate-600 ring-1 ring-slate-300 hover:bg-slate-50"
              @click="cancelEdit"
            >
              Vazgeç
            </button>
          </div>
        </form>
      </section>
    </div>
  </main>
</template>
