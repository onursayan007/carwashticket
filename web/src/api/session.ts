import { ofetch } from 'ofetch'
import { ref } from 'vue'
import type { AuthResponse, UserDto } from '@/types'

export const baseURL = import.meta.env.VITE_API_BASE_URL

// Access token bilerek sadece bellekte tutuluyor: localStorage'a yazılırsa
// XSS ile çalınabilir. Sayfa yenilendiğinde httpOnly cookie ile geri alınır.
export const accessToken = ref<string | null>(null)
export const currentUser = ref<UserDto | null>(null)

export function setSession(auth: AuthResponse): void {
  accessToken.value = auth.accessToken
  currentUser.value = auth.user
}

export function clearSession(): void {
  accessToken.value = null
  currentUser.value = null
}

// Aynı anda birden fazla istek 401 alırsa tek bir refresh çağrısı yapılır.
let refreshPromise: Promise<boolean> | null = null

async function performRefresh(): Promise<boolean> {
  try {
    const auth = await ofetch<AuthResponse>('/api/auth/refresh', {
      baseURL,
      method: 'POST',
      credentials: 'include',
    })

    setSession(auth)
    return true
  } catch {
    clearSession()
    return false
  }
}

export function refreshSession(): Promise<boolean> {
  refreshPromise ??= performRefresh().finally(() => {
    refreshPromise = null
  })

  return refreshPromise
}
