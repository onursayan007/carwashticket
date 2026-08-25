<template>
  <main class="flex min-h-dvh flex-col items-center justify-center gap-4 bg-brand-mist p-6 text-center">
    <p class="text-5xl" aria-hidden="true">🚫</p>
    <h1 class="text-lg font-bold text-brand-navy">Bu sayfaya erişiminiz yok</h1>
    <p class="max-w-sm text-sm text-slate-500">
      Hesabınızın rolü bu ekranı görüntüleyemiyor.
    </p>
    <RouterLink
      :to="{ name: 'login' }"
      class="rounded-lg bg-brand-blue px-5 py-2.5 font-semibold text-white transition hover:bg-brand-blue-dark"
    >
      Giriş ekranına dön
    </RouterLink>
  </main>
</template>
