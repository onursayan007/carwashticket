<script setup lang="ts">
import { Html5Qrcode } from 'html5-qrcode'
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiFetch, errorMessage } from '@/api/client'
import { useAuthStore } from '@/stores/auth'
import type { RedeemTicketResponse } from '@/types'

const auth = useAuthStore()
const router = useRouter()

const READER_ID = 'qr-reader'

type Phase = 'starting' | 'scanning' | 'checking' | 'result'

const phase = ref<Phase>('starting')
const result = ref<RedeemTicketResponse | null>(null)
const cameraBlocked = ref(false)
const manualCode = ref('')

let scanner: Html5Qrcode | null = null
// Kamera aynı kodu saniyede birkaç kez okur; ilk okumadan sonra kapatıyoruz.
let handled = false

async function stopScanner() {
  if (!scanner) {
    return
  }

  try {
    await scanner.stop()
  } catch {
    // Zaten durmuşsa sorun değil.
  }

  scanner.clear()
  scanner = null
}

async function redeem(code: string) {
  phase.value = 'checking'

  try {
    result.value = await apiFetch<RedeemTicketResponse>('/api/tickets/redeem', {
      method: 'POST',
      body: { code },
    })
  } catch (err) {
    result.value = {
      success: false,
      message: errorMessage(err, 'Bilet doğrulanamadı.'),
      serviceName: null,
      redeemedAt: null,
    }
  } finally {
    phase.value = 'result'
  }
}

async function startCamera() {
  handled = false
  cameraBlocked.value = false
  phase.value = 'starting'

  try {
    scanner = new Html5Qrcode(READER_ID)

    await scanner.start(
      { facingMode: 'environment' },
      { fps: 10, qrbox: { width: 260, height: 260 } },
      async (decodedText) => {
        if (handled) {
          return
        }

        handled = true
        await stopScanner()
        await redeem(decodedText.trim())
      },
      // Her karede çağrılır, okunamayan kare hata değildir.
      () => {},
    )

    phase.value = 'scanning'
  } catch {
    // İzin reddedildi veya kamera yok; manuel girişe düşüyoruz.
    cameraBlocked.value = true
    phase.value = 'result'
    scanner = null
  }
}

async function submitManual() {
  const code = manualCode.value.trim()

  if (code) {
    await redeem(code)
  }
}

async function scanAgain() {
  result.value = null
  manualCode.value = ''
  await startCamera()
}

function onLogout() {
  auth.logout()
  router.replace({ name: 'login' })
}

onMounted(startCamera)
onUnmounted(stopScanner)
</script>

<template>
  <main class="min-h-dvh bg-brand-navy-dark text-white">
    <header class="flex items-center justify-between px-4 py-3">
      <h1 class="text-lg font-semibold">Bilet oku</h1>
      <button type="button" class="text-sm text-slate-400 hover:text-white" @click="onLogout">
        Çıkış
      </button>
    </header>

    <div class="mx-auto max-w-md space-y-4 p-4">
      <!-- Kamera burada açılıyor; sonuç ekranındayken gizleniyor ama DOM'da kalıyor. -->
      <div
        v-show="phase === 'starting' || phase === 'scanning'"
        :id="READER_ID"
        class="overflow-hidden rounded-xl bg-black"
      />

      <p v-if="phase === 'starting'" class="text-center text-sm text-slate-400">
        Kamera açılıyor…
      </p>

      <p v-else-if="phase === 'scanning'" class="text-center text-sm text-slate-400">
        QR kodu çerçeveye getirin.
      </p>

      <p v-else-if="phase === 'checking'" class="py-12 text-center text-lg">
        Kontrol ediliyor…
      </p>

      <template v-else-if="phase === 'result'">
        <div
          v-if="result"
          class="rounded-2xl p-8 text-center"
          :class="result.success ? 'bg-brand-green' : 'bg-red-600'"
          role="status"
          aria-live="assertive"
        >
          <p class="text-6xl" aria-hidden="true">{{ result.success ? '✓' : '✕' }}</p>
          <p class="mt-4 text-2xl font-bold">
            {{ result.success ? 'Geçerli' : 'Geçersiz' }}
          </p>
          <p v-if="result.serviceName" class="mt-2 text-lg font-medium">
            {{ result.serviceName }}
          </p>
          <p class="mt-2 text-sm opacity-90">{{ result.message }}</p>
        </div>

        <div
          v-if="cameraBlocked"
          class="rounded-xl bg-white/10 p-4 text-sm text-white/80"
          role="alert"
        >
          Kameraya erişilemedi. Kodu elle girebilirsiniz.
        </div>

        <button
          v-if="!cameraBlocked"
          type="button"
          class="w-full rounded-lg bg-white px-4 py-3 text-lg font-semibold text-brand-navy"
          @click="scanAgain"
        >
          Yeni bilet oku
        </button>
      </template>

      <!-- Manuel giriş her zaman açık: kamera çalışsa da bozuk QR olabilir. -->
      <form class="space-y-2 pt-4" @submit.prevent="submitManual">
        <label for="manual-code" class="block text-sm text-slate-400">Kodu elle gir</label>
        <div class="flex gap-2">
          <input
            id="manual-code"
            v-model="manualCode"
            type="text"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            placeholder="Bilet kodu"
            class="min-w-0 flex-1 rounded-lg bg-white/10 px-3 py-2 text-white placeholder:text-slate-500 outline-none focus:ring-2 focus:ring-white"
          />
          <button
            type="submit"
            :disabled="!manualCode.trim() || phase === 'checking'"
            class="shrink-0 rounded-lg bg-white px-4 py-2 font-semibold text-brand-navy disabled:opacity-40"
          >
            Kontrol et
          </button>
        </div>
      </form>
    </div>
  </main>
</template>
