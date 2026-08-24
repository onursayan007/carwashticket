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
  <main class="flex min-h-screen items-center justify-center bg-slate-50 p-4">
    <form
      class="w-full max-w-sm space-y-5 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200"
      @submit.prevent="onSubmit"
    >
      <div>
        <h1 class="text-xl font-semibold text-slate-900">Giriş yap</h1>
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
          class="w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-slate-900"
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
          class="w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-900 outline-none focus:border-slate-900"
        />
      </div>

      <button
        type="submit"
        :disabled="submitting"
        class="w-full rounded-lg bg-slate-900 px-4 py-2 font-medium text-white hover:bg-slate-800 disabled:opacity-50"
      >
        {{ submitting ? 'Giriş yapılıyor…' : 'Giriş yap' }}
      </button>
    </form>
  </main>
</template>
