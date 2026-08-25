<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import LocationPicker from '@/components/LocationPicker.vue'
import { useAuthStore } from '@/stores/auth'
import type { BusinessSummaryDto, CreateBusinessRequest, CreateBusinessResponse } from '@/types'

const auth = useAuthStore()
const router = useRouter()

const businesses = ref<BusinessSummaryDto[]>([])
const loading = ref(true)
const listError = ref<string | null>(null)

const showForm = ref(false)
const saving = ref(false)
const formError = ref<string | null>(null)
const success = ref<string | null>(null)

function emptyForm(): CreateBusinessRequest {
  return {
    name: '',
    type: 'SelfService',
    companyName: null,
    taxNumber: null,
    taxOffice: null,
    address: null,
    city: '',
    district: '',
    // Antalya merkez: harita boş açılmasın.
    latitude: 36.8969,
    longitude: 30.7133,
    contactEmail: '',
    phoneNumber: null,
  }
}

const form = ref<CreateBusinessRequest>(emptyForm())

function typeLabel(type: BusinessSummaryDto['type']): string {
  return type === 'SelfService' ? 'Self servis' : type === 'FullService' ? 'Tam hizmet' : 'Karma'
}

function onPick(lat: number, lng: number) {
  form.value.latitude = Number(lat.toFixed(6))
  form.value.longitude = Number(lng.toFixed(6))
}

async function load() {
  loading.value = true
  listError.value = null

  try {
    businesses.value = await apiFetch<BusinessSummaryDto[]>('/api/admin/businesses')
  } catch (err) {
    listError.value = errorMessage(err, 'İşyerleri yüklenemedi.')
  } finally {
    loading.value = false
  }
}

async function submit() {
  saving.value = true
  formError.value = null
  success.value = null

  try {
    const result = await apiFetch<CreateBusinessResponse>('/api/admin/businesses', {
      method: 'POST',
      body: form.value,
    })

    success.value = `${result.ownerEmail} adresine geçici şifre gönderildi.`
    form.value = emptyForm()
    showForm.value = false
    await load()
  } catch (err) {
    formError.value = errorMessage(err, 'İşyeri oluşturulamadı.')
  } finally {
    saving.value = false
  }
}

function onLogout() {
  auth.logout()
  router.replace({ name: 'login' })
}

onMounted(load)
</script>

<template>
  <main class="min-h-dvh bg-slate-100">
    <header class="bg-brand-navy px-4 py-3 text-white">
      <div class="mx-auto flex max-w-4xl items-center justify-between">
        <div>
          <h1 class="font-semibold">Platform yönetimi</h1>
          <p class="text-xs text-white/70">İşyeri kayıtları</p>
        </div>
        <button type="button" class="text-sm text-white/80 hover:text-white" @click="onLogout">
          Çıkış
        </button>
      </div>
    </header>

    <div class="mx-auto max-w-4xl space-y-4 p-4">
      <p
        v-if="success"
        class="rounded-lg border border-green-200 bg-green-50 px-3 py-2 text-sm text-brand-green"
        role="status"
      >
        {{ success }}
      </p>

      <div class="flex items-center justify-between">
        <h2 class="font-semibold text-slate-900">
          İşyerleri
          <span v-if="!loading" class="font-normal text-slate-500">({{ businesses.length }})</span>
        </h2>

        <button
          type="button"
          class="rounded-lg bg-brand-blue px-4 py-2 text-sm font-semibold text-white transition hover:bg-brand-blue-dark"
          @click="showForm = !showForm"
        >
          {{ showForm ? 'Vazgeç' : '+ Yeni işyeri' }}
        </button>
      </div>

      <!-- Yeni işyeri formu -->
      <form
        v-if="showForm"
        class="space-y-4 rounded-xl border border-slate-200 bg-white p-4"
        @submit.prevent="submit"
      >
        <p
          v-if="formError"
          class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
          role="alert"
        >
          {{ formError }}
        </p>

        <div class="grid gap-3 sm:grid-cols-2">
          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">İşyeri adı *</span>
            <input
              v-model="form.name"
              type="text"
              required
              maxlength="200"
              placeholder="Elmalı Petrol Self Servis"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">Hizmet türü *</span>
            <select
              v-model="form.type"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            >
              <option value="SelfService">Self servis</option>
              <option value="FullService">Tam hizmet</option>
              <option value="Both">Karma</option>
            </select>
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">Ticari unvan</span>
            <input
              v-model="form.companyName"
              type="text"
              maxlength="300"
              placeholder="Elmalı Akaryakıt Ltd. Şti."
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">Vergi no</span>
            <input
              v-model="form.taxNumber"
              type="text"
              maxlength="20"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">Vergi dairesi</span>
            <input
              v-model="form.taxOffice"
              type="text"
              maxlength="150"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">Telefon</span>
            <input
              v-model="form.phoneNumber"
              type="tel"
              maxlength="30"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">İl</span>
            <input
              v-model="form.city"
              type="text"
              maxlength="100"
              placeholder="Antalya"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>

          <label class="space-y-1">
            <span class="block text-sm font-medium text-slate-700">İlçe</span>
            <input
              v-model="form.district"
              type="text"
              maxlength="100"
              placeholder="Elmalı"
              class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
            />
          </label>
        </div>

        <label class="block space-y-1">
          <span class="block text-sm font-medium text-slate-700">Adres</span>
          <input
            v-model="form.address"
            type="text"
            maxlength="500"
            class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
          />
        </label>

        <label class="block space-y-1">
          <span class="block text-sm font-medium text-slate-700">
            İşyeri e-postası *
            <span class="font-normal text-slate-500">— geçici şifre buraya gider</span>
          </span>
          <input
            v-model="form.contactEmail"
            type="email"
            required
            maxlength="256"
            placeholder="isyeri@ornek.com"
            class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue"
          />
        </label>

        <!-- Konum: haritaya dokunarak veya pini sürükleyerek -->
        <div class="space-y-2">
          <p class="text-sm font-medium text-slate-700">
            Konum
            <span class="font-normal text-slate-500">— haritaya dokunun veya pini sürükleyin</span>
          </p>

          <LocationPicker
            :latitude="form.latitude"
            :longitude="form.longitude"
            @pick="onPick"
          />

          <div class="grid grid-cols-2 gap-3">
            <label class="space-y-1">
              <span class="block text-xs text-slate-500">Enlem</span>
              <input
                v-model.number="form.latitude"
                type="number"
                step="0.000001"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm tabular-nums outline-none focus:border-brand-blue"
              />
            </label>
            <label class="space-y-1">
              <span class="block text-xs text-slate-500">Boylam</span>
              <input
                v-model.number="form.longitude"
                type="number"
                step="0.000001"
                required
                class="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm tabular-nums outline-none focus:border-brand-blue"
              />
            </label>
          </div>
        </div>

        <button
          type="submit"
          :disabled="saving"
          class="w-full rounded-lg bg-brand-blue px-4 py-3 font-semibold text-white transition hover:bg-brand-blue-dark disabled:opacity-50"
        >
          {{ saving ? 'Kaydediliyor…' : 'İşyerini oluştur ve şifre gönder' }}
        </button>
      </form>

      <!-- Liste -->
      <p v-if="loading" class="py-8 text-center text-sm text-slate-500">Yükleniyor…</p>

      <p
        v-else-if="listError"
        class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
        role="alert"
      >
        {{ listError }}
      </p>

      <p v-else-if="businesses.length === 0" class="py-8 text-center text-sm text-slate-500">
        Henüz işyeri yok.
      </p>

      <ul v-else class="space-y-2">
        <li
          v-for="business in businesses"
          :key="business.id"
          class="rounded-xl border border-slate-200 bg-white p-4"
        >
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <p class="font-semibold text-brand-navy">{{ business.name }}</p>
              <p v-if="business.companyName" class="text-sm text-slate-500">
                {{ business.companyName }}
              </p>
              <p class="mt-1 text-xs text-slate-500">
                {{ business.district }}<template v-if="business.city">, {{ business.city }}</template>
                · {{ business.contactEmail }}
              </p>
            </div>

            <div class="flex shrink-0 flex-col items-end gap-1">
              <span
                class="rounded-md bg-brand-blue-soft px-2 py-0.5 text-[11px] font-medium text-brand-navy"
              >
                {{ typeLabel(business.type) }}
              </span>
              <span class="text-xs text-slate-500">{{ business.serviceCount }} hizmet</span>
              <span v-if="!business.isActive" class="text-xs font-medium text-red-600">Pasif</span>
            </div>
          </div>

          <p class="mt-2 font-mono text-[11px] text-slate-400">
            {{ business.latitude.toFixed(4) }}, {{ business.longitude.toFixed(4) }}
          </p>
        </li>
      </ul>
    </div>
  </main>
</template>
