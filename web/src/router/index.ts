import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { Role } from '@/types'

declare module 'vue-router' {
  interface RouteMeta {
    // Giriş gerektirmeyen rotalar.
    public?: boolean
    // Boşsa sadece giriş yeterli; doluysa bu rollerden biri gerekir.
    roles?: readonly Role[]
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    name: 'stations',
    component: () => import('@/views/StationsView.vue'),
    meta: { roles: ['Customer'] },
  },
  {
    path: '/scan',
    name: 'scan',
    component: () => import('@/views/ScanView.vue'),
    meta: { roles: ['Staff'] },
  },
  {
    path: '/manage',
    name: 'manage',
    component: () => import('@/views/ManageView.vue'),
    meta: { roles: ['Manager'] },
  },
  {
    path: '/forbidden',
    name: 'forbidden',
    component: () => import('@/views/ForbiddenView.vue'),
    meta: { public: true },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: { name: 'stations' },
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Buradaki kontrol sadece gezinme kolaylığı; asıl yetkilendirme backend'de.
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.initialize()

  if (to.meta.public) {
    return true
  }

  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  const required = to.meta.roles

  if (required && required.length > 0 && !auth.hasAnyRole(required)) {
    return { name: 'forbidden' }
  }

  return true
})
