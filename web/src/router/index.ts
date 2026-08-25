import { createRouter, createWebHistory, type RouteLocationRaw, type RouteRecordRaw } from 'vue-router'
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
    component: () => import('@/views/DiscoverView.vue'),
    meta: { roles: ['Customer'] },
  },
  {
    path: '/stations/:id',
    name: 'station-detail',
    component: () => import('@/views/StationDetailView.vue'),
    props: true,
    meta: { roles: ['Customer'] },
  },
  {
    path: '/odeme/sonuc',
    name: 'payment-result',
    component: () => import('@/views/PaymentResultView.vue'),
    meta: { roles: ['Customer'] },
  },
  {
    path: '/biletlerim',
    name: 'wallet',
    component: () => import('@/views/WalletView.vue'),
    meta: { roles: ['Customer'] },
  },
  {
    path: '/scan',
    name: 'scan',
    component: () => import('@/views/ScannerView.vue'),
    meta: { roles: ['Scanner'] },
  },
  {
    path: '/manage',
    name: 'manage',
    component: () => import('@/views/PanelView.vue'),
    meta: { roles: ['Business'] },
  },
  {
    path: '/admin',
    name: 'admin',
    component: () => import('@/views/AdminView.vue'),
    meta: { roles: ['Admin'] },
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

// Rol başına açılış ekranı. Birden fazla rolü olana en yetkilisi verilir.
export function homeRouteFor(roles: readonly Role[]): RouteLocationRaw {
  if (roles.includes('Admin')) return { name: 'admin' }
  if (roles.includes('Business')) return { name: 'manage' }
  if (roles.includes('Scanner')) return { name: 'scan' }
  if (roles.includes('Customer')) return { name: 'stations' }

  return { name: 'forbidden' }
}

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Buradaki kontrol sadece gezinme kolaylığı; asıl yetkilendirme backend'de.
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.initialize()

  if (to.name === 'login' && auth.isAuthenticated) {
    return homeRouteFor(auth.roles)
  }

  if (to.meta.public) {
    return true
  }

  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  const required = to.meta.roles

  // Rolü uymayan kullanıcı hata sayfası yerine kendi ekranına gönderilir.
  if (required && required.length > 0 && !auth.hasAnyRole(required)) {
    return homeRouteFor(auth.roles)
  }

  return true
})
