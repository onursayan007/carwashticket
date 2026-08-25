<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { errorMessage } from '@/api/client'
import { ofetch } from 'ofetch'

const route = useRoute()
const router = useRouter()

const providerRef = computed(() => String(route.query.ref ?? ''))
const orderId = computed(() => String(route.query.orderId ?? ''))

const amount = computed(() => {
  const raw = Number(route.query.amount)

  return Number.isFinite(raw) ? raw : null
})

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })

// Kart bilgileri SADECE bu ekranda kalır; hiçbir yere gönderilmez.
const cardNumber = ref('')
const expiry = ref('')
const cvc = ref('')
const holder = ref('')

const submitting = ref(false)
const error = ref<string | null>(null)

const digits = computed(() => cardNumber.value.replace(/\D/g, ''))

const valid = computed(
  () =>
    digits.value.length === 16 &&
    /^(0[1-9]|1[0-2])\/\d{2}$/.test(expiry.value) &&
    /^\d{3}$/.test(cvc.value) &&
    holder.value.trim().length >= 3,
)

const masked = computed(() =>
  (digits.value.padEnd(16, '•').match(/.{1,4}/g) ?? []).join(' '),
)

function onCardInput(event: Event) {
  const raw = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(0, 16)
  cardNumber.value = (raw.match(/.{1,4}/g) ?? []).join(' ')
}

function onExpiryInput(event: Event) {
  const raw = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(0, 4)
  expiry.value = raw.length > 2 ? `${raw.slice(0, 2)}/${raw.slice(2)}` : raw
}

// Bu uç kimlik doğrulaması istemiyor (gerçek banka dönüşü gibi),
// o yüzden apiFetch yerine doğrudan ofetch kullanıyoruz.
async function send(outcome: 'success' | 'fail') {
  if (submitting.value) {
    return
  }

  submitting.value = true
  error.value = null

  try {
    await ofetch('/api/payments/mock-callback', {
      baseURL: import.meta.env.VITE_API_BASE_URL,
      method: 'POST',
      body: { providerRef: providerRef.value, outcome },
    })

    await router.replace({
      name: 'payment-result',
      query: { orderId: orderId.value, status: outcome === 'success' ? 'pending' : 'failed' },
    })
  } catch (err) {
    error.value = errorMessage(err, 'Ödeme sonucu gönderilemedi.')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="min-h-dvh bg-slate-200">
    <!-- Test uyarısı her zaman görünür, sayfayla birlikte kayar değil sabit. -->
    <div
      class="sticky top-0 z-10 bg-brand-sponge px-4 py-2.5 text-center text-sm font-bold text-brand-navy-dark"
      role="alert"
    >
      ⚠️ TEST ORTAMI — gerçek ödeme alınmaz, kart bilgisi hiçbir yere kaydedilmez
    </div>

    <div class="mx-auto max-w-md p-4">
      <div class="overflow-hidden rounded-2xl bg-white shadow-xl">
        <!-- Banka başlığı görünümü -->
        <div class="flex items-center justify-between bg-brand-navy px-5 py-4 text-white">
          <div>
            <p class="text-sm font-semibold">3D Secure Doğrulama</p>
            <p class="text-xs text-white/70">Güvenli ödeme sayfası</p>
          </div>
          <span class="text-2xl" aria-hidden="true">🔒</span>
        </div>

        <div v-if="!providerRef" class="p-6 text-center text-sm text-slate-500">
          Ödeme referansı bulunamadı.
        </div>

        <template v-else>
          <!-- Kart görseli -->
          <div class="p-5">
            <div
              class="rounded-2xl bg-gradient-to-br from-brand-navy to-brand-blue p-5 text-white shadow-lg"
            >
              <div class="flex items-start justify-between">
                <span class="h-8 w-11 rounded-md bg-brand-sponge/80" aria-hidden="true" />
                <span class="text-xs font-semibold tracking-widest text-white/70">TEST</span>
              </div>

              <p class="mt-6 font-mono text-lg tracking-widest tabular-nums">
                {{ masked }}
              </p>

              <div class="mt-4 flex items-end justify-between text-xs">
                <span class="min-w-0">
                  <span class="block text-white/60">KART SAHİBİ</span>
                  <span class="block truncate font-medium uppercase">
                    {{ holder || '—' }}
                  </span>
                </span>
                <span>
                  <span class="block text-white/60">SON KUL.</span>
                  <span class="block font-medium tabular-nums">{{ expiry || 'MM/YY' }}</span>
                </span>
              </div>
            </div>
          </div>

          <!-- Tutar -->
          <div class="mx-5 flex items-center justify-between rounded-xl bg-brand-mist px-4 py-3">
            <span class="text-sm text-slate-600">Ödenecek tutar</span>
            <span class="text-lg font-bold text-brand-navy">
              {{ amount !== null ? money.format(amount) : '—' }}
            </span>
          </div>

          <form class="space-y-4 p-5" @submit.prevent="send('success')">
            <p
              v-if="error"
              class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
              role="alert"
            >
              {{ error }}
            </p>

            <label class="block space-y-1">
              <span class="block text-sm font-medium text-slate-700">Kart numarası</span>
              <input
                :value="cardNumber"
                type="text"
                inputmode="numeric"
                autocomplete="off"
                placeholder="5528 7900 0000 0008"
                class="w-full rounded-lg border border-slate-300 px-3 py-2.5 font-mono tabular-nums outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
                @input="onCardInput"
              />
            </label>

            <div class="grid grid-cols-2 gap-3">
              <label class="space-y-1">
                <span class="block text-sm font-medium text-slate-700">Son kullanma</span>
                <input
                  :value="expiry"
                  type="text"
                  inputmode="numeric"
                  autocomplete="off"
                  placeholder="12/28"
                  class="w-full rounded-lg border border-slate-300 px-3 py-2.5 font-mono tabular-nums outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
                  @input="onExpiryInput"
                />
              </label>

              <label class="space-y-1">
                <span class="block text-sm font-medium text-slate-700">CVC</span>
                <input
                  v-model="cvc"
                  type="text"
                  inputmode="numeric"
                  maxlength="3"
                  autocomplete="off"
                  placeholder="123"
                  class="w-full rounded-lg border border-slate-300 px-3 py-2.5 font-mono tabular-nums outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
                />
              </label>
            </div>

            <label class="block space-y-1">
              <span class="block text-sm font-medium text-slate-700">Kart üzerindeki isim</span>
              <input
                v-model="holder"
                type="text"
                autocomplete="off"
                placeholder="AD SOYAD"
                class="w-full rounded-lg border border-slate-300 px-3 py-2.5 uppercase outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
              />
            </label>

            <button
              type="submit"
              :disabled="!valid || submitting"
              class="w-full rounded-lg bg-brand-green px-4 py-3.5 font-semibold text-white transition hover:brightness-110 disabled:opacity-40"
            >
              {{ submitting ? 'İşleniyor…' : 'Ödemeyi Onayla' }}
            </button>

            <!-- Başarısız senaryoyu da görebilmek için: form geçerli olmasa da çalışır. -->
            <button
              type="button"
              :disabled="submitting"
              class="w-full rounded-lg border border-red-300 px-4 py-3 font-semibold text-red-700 transition hover:bg-red-50 disabled:opacity-40"
              @click="send('fail')"
            >
              Ödemeyi Reddet
            </button>

            <p class="text-center text-xs text-slate-400">
              Girilen kart bilgileri sunucuya gönderilmez.
            </p>
          </form>
        </template>
      </div>
    </div>
  </main>
</template>
