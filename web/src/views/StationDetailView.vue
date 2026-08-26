<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { apiFetch, errorMessage } from '@/api/client'
import StarRating from '@/components/StarRating.vue'
import { serviceLook } from '@/serviceLook'
import type { CreateOrderResponse, ReviewDto, ServiceDto, StationDetailDto } from '@/types'

const props = defineProps<{ id: string }>()

const MAX_PER_ITEM = 20

const station = ref<StationDetailDto | null>(null)
const error = ref<string | null>(null)
const loading = ref(true)

// Hizmet kimliği -> adet. Sepetin tamamı burada.
const quantities = ref<Record<string, number>>({})

const checkoutError = ref<string | null>(null)
const submitting = ref(false)

// Sepet değişince yenileniyor: aynı sepet için tekrar denemeler tek sipariş sayılır,
// sepet değişirse yeni bir sipariş olur.
const idempotencyKey = ref(crypto.randomUUID())

const money = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })

const cart = computed(() =>
  (station.value?.services ?? [])
    .map((service) => ({ service, quantity: quantities.value[service.id] ?? 0 }))
    .filter((line) => line.quantity > 0),
)

const itemCount = computed(() => cart.value.reduce((sum, line) => sum + line.quantity, 0))

const total = computed(() =>
  cart.value.reduce((sum, line) => sum + line.service.price * line.quantity, 0),
)

// Self serviste birim, tam hizmette paket satılıyor. İkisi varsa ayrı başlık altında.
const groups = computed(() => {
  const services = station.value?.services ?? []

  return [
    { title: 'Birim seçin', hint: 'İstediğiniz kadar ekleyin', items: services.filter((s) => s.kind === 'Unit') },
    { title: 'Paketler', hint: 'Aracınızı teslim edin', items: services.filter((s) => s.kind === 'Package') },
  ].filter((group) => group.items.length > 0)
})

function quantityOf(service: ServiceDto): number {
  return quantities.value[service.id] ?? 0
}

function setQuantity(service: ServiceDto, next: number) {
  const clamped = Math.max(0, Math.min(MAX_PER_ITEM, next))

  if (clamped === 0) {
    delete quantities.value[service.id]
  } else {
    quantities.value[service.id] = clamped
  }
}

const reviews = ref<ReviewDto[]>([])

const reviewDate = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' })

onMounted(async () => {
  // Yorumlar gelmezse sayfa yine çalışsın; ikisi ayrı istek.
  apiFetch<ReviewDto[]>(`/api/stations/${props.id}/reviews`)
    .then((result) => (reviews.value = result))
    .catch(() => (reviews.value = []))

  try {
    station.value = await apiFetch<StationDetailDto>(`/api/stations/${props.id}`)
  } catch (err) {
    error.value = errorMessage(err, 'İstasyon bilgisi yüklenemedi.')
  } finally {
    loading.value = false
  }
})

watch(
  quantities,
  () => {
    idempotencyKey.value = crypto.randomUUID()
    checkoutError.value = null
  },
  { deep: true },
)

async function startCheckout() {
  if (!station.value || cart.value.length === 0 || submitting.value) {
    return
  }

  submitting.value = true
  checkoutError.value = null

  try {
    const order = await apiFetch<CreateOrderResponse>('/api/orders', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey.value },
      body: {
        stationId: station.value.id,
        items: cart.value.map((line) => ({
          serviceId: line.service.id,
          quantity: line.quantity,
        })),
      },
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
  <main class="min-h-dvh bg-brand-mist pb-40">
    <header
      class="sticky top-0 z-10 flex items-center gap-3 bg-brand-navy px-4 py-3 text-white"
    >
      <RouterLink
        :to="{ name: 'stations' }"
        class="grid h-9 w-9 shrink-0 place-items-center rounded-lg hover:bg-white/15"
        aria-label="Geri"
      >
        ←
      </RouterLink>
      <h1 class="min-w-0 flex-1 truncate font-semibold">
        {{ station?.name ?? 'Yükleniyor…' }}
      </h1>
    </header>

    <p v-if="loading" class="py-16 text-center text-sm text-slate-500">Yükleniyor…</p>

    <p
      v-else-if="error"
      class="mx-4 mt-4 rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700"
      role="alert"
    >
      {{ error }}
    </p>

    <div v-else-if="station" class="mx-auto max-w-2xl">
      <!-- İşyeri künyesi -->
      <section class="border-b border-slate-200 bg-white px-4 py-4">
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <h2 class="text-lg font-bold text-brand-navy">{{ station.name }}</h2>
            <p v-if="station.address" class="mt-0.5 text-sm text-slate-500">
              {{ station.address }}
            </p>
          </div>
          <span
            class="shrink-0 rounded-md bg-brand-sky px-2.5 py-1 text-xs font-semibold text-brand-navy"
          >
            {{ station.type === 'SelfService' ? 'Self servis' : station.type === 'FullService' ? 'Tam hizmet' : 'Karma' }}
          </span>
        </div>

        <div class="mt-3 flex items-center gap-2">
          <span
            class="rounded-md rounded-bl-none bg-brand-navy px-2 py-1 text-sm font-bold text-white tabular-nums"
          >
            {{ station.ratingAverage.toFixed(1) }}
          </span>
          <span class="text-sm text-slate-500">
            {{ station.ratingCount }} değerlendirme
          </span>
        </div>
      </section>

      <!-- Hizmetler -->
      <div class="space-y-6 p-4">
        <section v-for="group in groups" :key="group.title">
          <div class="mb-2 flex items-baseline justify-between">
            <h3 class="text-sm font-bold text-brand-navy">{{ group.title }}</h3>
            <p class="text-xs text-slate-400">{{ group.hint }}</p>
          </div>

          <ul class="space-y-2">
            <li
              v-for="service in group.items"
              :key="service.id"
              class="flex items-center gap-3 rounded-xl border bg-white p-3 transition"
              :class="
                quantityOf(service) > 0
                  ? 'border-brand-blue shadow-sm'
                  : 'border-brand-navy/15'
              "
            >
              <span
                class="grid h-11 w-11 shrink-0 place-items-center rounded-xl text-xl"
                :class="serviceLook(service.name).chip"
                aria-hidden="true"
              >
                {{ serviceLook(service.name).icon }}
              </span>

              <div class="min-w-0 flex-1">
                <p class="font-semibold text-brand-navy">{{ service.name }}</p>
                <p v-if="service.description" class="mt-0.5 text-xs text-slate-500">
                  {{ service.description }}
                </p>
                <p class="mt-1 text-sm font-bold text-brand-navy">
                  {{ money.format(service.price) }}
                  <span class="text-xs font-normal text-slate-400">
                    · ~{{ service.durationMinutes }} dk
                  </span>
                </p>
              </div>

              <!-- Seçilmemişse sadece +, seçilmişse adet kontrolü -->
              <button
                v-if="quantityOf(service) === 0"
                type="button"
                class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-blue text-xl font-light text-white transition hover:bg-brand-blue-dark"
                :aria-label="`${service.name} ekle`"
                @click="setQuantity(service, 1)"
              >
                +
              </button>

              <div
                v-else
                class="flex shrink-0 items-center gap-1 rounded-full bg-brand-blue p-1 text-white"
              >
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-full text-lg font-light transition hover:bg-white/15"
                  :aria-label="`${service.name} azalt`"
                  @click="setQuantity(service, quantityOf(service) - 1)"
                >
                  −
                </button>
                <span class="min-w-6 text-center text-sm font-semibold tabular-nums">
                  {{ quantityOf(service) }}
                </span>
                <button
                  type="button"
                  class="grid h-8 w-8 place-items-center rounded-full text-lg font-light transition hover:bg-white/15 disabled:opacity-40"
                  :disabled="quantityOf(service) >= MAX_PER_ITEM"
                  :aria-label="`${service.name} artır`"
                  @click="setQuantity(service, quantityOf(service) + 1)"
                >
                  +
                </button>
              </div>
            </li>
          </ul>
        </section>

        <p v-if="station.services.length === 0" class="py-8 text-center text-sm text-slate-500">
          Bu işyerinde tanımlı hizmet yok.
        </p>

        <!-- Değerlendirmeler -->
        <section v-if="reviews.length > 0">
          <h3 class="mb-2 text-sm font-bold text-brand-navy">Değerlendirmeler</h3>

          <ul class="space-y-2">
            <li
              v-for="review in reviews"
              :key="review.id"
              class="rounded-xl border border-brand-navy/15 bg-white p-3"
            >
              <div class="flex items-center justify-between gap-3">
                <StarRating :model-value="review.rating" readonly />
                <span class="text-xs text-slate-400">
                  {{ reviewDate.format(new Date(review.createdAt)) }}
                </span>
              </div>

              <p v-if="review.comment" class="mt-2 text-sm text-slate-700">
                {{ review.comment }}
              </p>
              <p class="mt-1 text-xs font-medium text-slate-500">{{ review.authorLabel }}</p>
            </li>
          </ul>
        </section>
      </div>
    </div>

    <!-- Sepet çubuğu -->
    <Transition
      enter-active-class="transition duration-200"
      enter-from-class="translate-y-full"
      leave-active-class="transition duration-200"
      leave-to-class="translate-y-full"
    >
      <div
        v-if="itemCount > 0"
        class="fixed inset-x-0 bottom-0 z-20 border-t border-slate-200 bg-white/95 p-3 backdrop-blur"
      >
        <div class="mx-auto max-w-2xl space-y-2">
          <p
            v-if="checkoutError"
            class="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700"
            role="alert"
          >
            {{ checkoutError }}
          </p>

          <!-- Sepetin dökümü: "2 x Su · 1 x Köpük" -->
          <p class="truncate px-1 text-xs text-slate-500">
            {{ cart.map((l) => `${l.quantity} × ${l.service.name}`).join(' · ') }}
          </p>

          <button
            type="button"
            :disabled="submitting"
            class="flex w-full items-center justify-between gap-3 rounded-xl bg-brand-blue px-4 py-3.5 text-white transition hover:bg-brand-blue-dark disabled:opacity-50"
            @click="startCheckout"
          >
            <span
              class="grid h-7 min-w-7 place-items-center rounded-full bg-white/20 px-2 text-sm font-semibold tabular-nums"
            >
              {{ itemCount }}
            </span>
            <span class="font-semibold">
              {{ submitting ? 'Yönlendiriliyor…' : 'Ödemeye geç' }}
            </span>
            <span class="font-semibold tabular-nums">{{ money.format(total) }}</span>
          </button>
        </div>
      </div>
    </Transition>
  </main>
</template>
