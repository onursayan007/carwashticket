import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { apiFetch } from '@/api/client'
import {
  accessToken,
  clearSession,
  currentUser,
  refreshSession,
  setSession,
} from '@/api/session'
import type { AuthResponse, LoginRequest, RegisterRequest, Role } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const initialized = ref(false)

  const token = computed(() => accessToken.value)
  const user = computed(() => currentUser.value)
  const roles = computed<Role[]>(() => currentUser.value?.roles ?? [])
  const isAuthenticated = computed(() => accessToken.value !== null && currentUser.value !== null)

  function hasAnyRole(allowed: readonly Role[]): boolean {
    return allowed.some((role) => roles.value.includes(role))
  }

  async function login(payload: LoginRequest): Promise<void> {
    const auth = await apiFetch<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: payload,
    })

    setSession(auth)
  }

  async function register(payload: RegisterRequest): Promise<void> {
    const auth = await apiFetch<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: payload,
    })

    setSession(auth)
  }

  function logout(): void {
    clearSession()
  }

  // Sayfa yenilendiğinde token bellekten silinir; cookie duruyorsa oturum geri gelir.
  async function initialize(): Promise<void> {
    if (initialized.value) {
      return
    }

    await refreshSession()
    initialized.value = true
  }

  return { token, user, roles, isAuthenticated, hasAnyRole, login, register, logout, initialize }
})
