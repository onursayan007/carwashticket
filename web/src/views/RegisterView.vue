<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { errorMessage } from '@/api/client'
import { homeRouteFor } from '@/router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()

const fullName = ref('')
const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const submitting = ref(false)

async function onSubmit() {
  error.value = null
  submitting.value = true

  try {
    await auth.register({
      email: email.value,
      password: password.value,
      fullName: fullName.value.trim() || null,
    })

    await router.replace(homeRouteFor(auth.roles))
  } catch (err) {
    error.value = errorMessage(err, 'Kayıt tamamlanamadı.')
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="flex min-h-dvh items-center justify-center bg-brand-navy p-4">
    <form
      class="w-full max-w-sm space-y-5 rounded-2xl bg-white p-6 shadow-2xl"
      @submit.prevent="onSubmit"
    >
      <div>
        <p class="text-3xl" aria-hidden="true">🫧</p>
        <h1 class="mt-2 text-xl font-bold text-brand-navy">Hesap oluştur</h1>
        <p class="mt-1 text-sm text-slate-500">Araç yıkama bileti</p>
      </div>

      <p v-if="error" class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700" role="alert">
        {{ error }}
      </p>

      <div class="space-y-1">
        <label for="fullName" class="block text-sm font-medium text-slate-700">Ad soyad</label>
        <input
          id="fullName"
          v-model="fullName"
          type="text"
          autocomplete="name"
          maxlength="200"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
        />
      </div>

      <div class="space-y-1">
        <label for="email" class="block text-sm font-medium text-slate-700">E-posta</label>
        <input
          id="email"
          v-model="email"
          type="email"
          autocomplete="email"
          required
          class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
        />
      </div>

      <div class="space-y-1">
        <label for="password" class="block text-sm font-medium text-slate-700">Şifre</label>
        <input
          id="password"
          v-model="password"
          type="password"
          autocomplete="new-password"
          required
          minlength="8"
          class="w-full rounded-lg border border-slate-300 px-3 py-2 outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
        />
        <p class="text-xs text-slate-400">
          En az 8 karakter, büyük ve küçük harf ile rakam içermeli.
        </p>
      </div>

      <button
        type="submit"
        :disabled="submitting"
        class="w-full rounded-lg bg-brand-blue px-4 py-3 font-semibold text-white transition hover:bg-brand-blue-dark disabled:opacity-50"
      >
        {{ submitting ? 'Oluşturuluyor…' : 'Hesap oluştur' }}
      </button>

      <p class="text-center text-sm text-slate-500">
        Zaten hesabın var mı?
        <RouterLink :to="{ name: 'login' }" class="font-semibold text-brand-blue">
          Giriş yap
        </RouterLink>
      </p>
    </form>
  </main>
</template>
