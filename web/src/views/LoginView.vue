<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { errorMessage } from '@/api/client'
import { homeRouteFor } from '@/router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const submitting = ref(false)

async function onSubmit() {
  error.value = null
  submitting.value = true

  try {
    await auth.login({ email: email.value, password: password.value })

    // Guard'ın gönderdiği adres varsa oraya, yoksa rolün açılış ekranına.
    const redirect = route.query.redirect
    await router.replace(
      typeof redirect === 'string' && redirect ? redirect : homeRouteFor(auth.roles),
    )
  } catch (err) {
    error.value = errorMessage(err, 'E-posta veya şifre hatalı.')
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
        <h1 class="mt-2 text-xl font-bold text-brand-navy">Giriş yap</h1>
        <p class="mt-1 text-sm text-slate-500">Araç yıkama bileti</p>
      </div>

      <p
        v-if="error"
        class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700"
        role="alert"
      >
        {{ error }}
      </p>

      <div class="space-y-1">
        <label for="email" class="block text-sm font-medium text-slate-700">E-posta</label>
        <input
          id="email"
          v-model="email"
          type="email"
          autocomplete="email"
          required
          class="w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
        />
      </div>

      <div class="space-y-1">
        <label for="password" class="block text-sm font-medium text-slate-700">Şifre</label>
        <input
          id="password"
          v-model="password"
          type="password"
          autocomplete="current-password"
          required
          class="w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-brand-blue focus:ring-2 focus:ring-brand-blue/20"
        />
      </div>

      <button
        type="submit"
        :disabled="submitting"
        class="w-full rounded-lg bg-brand-blue px-4 py-3 font-semibold text-white transition hover:bg-brand-blue-dark disabled:opacity-50"
      >
        {{ submitting ? 'Giriş yapılıyor…' : 'Giriş yap' }}
      </button>
    </form>
  </main>
</template>
