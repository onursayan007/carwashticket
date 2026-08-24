import { FetchError, ofetch, type FetchOptions } from 'ofetch'
import { accessToken, baseURL, clearSession, refreshSession } from './session'

// credentials: refresh cookie'sinin farklı origin'e gidebilmesi için.
const raw = ofetch.create({ baseURL, credentials: 'include' })

function withAuthHeader(headers: HeadersInit | undefined): Headers {
  const result = new Headers(headers)

  if (accessToken.value) {
    result.set('Authorization', `Bearer ${accessToken.value}`)
  }

  return result
}

function isUnauthorized(error: unknown): boolean {
  return error instanceof FetchError && error.statusCode === 401
}

// Kullanıcıya gösterilecek mesaj. ProblemDetails/ValidationProblemDetails ikisini de karşılar.
export function errorMessage(error: unknown, fallback = 'Bir hata oluştu.'): string {
  if (!(error instanceof FetchError)) {
    return fallback
  }

  const data = error.data as
    | { title?: string; detail?: string; errors?: Record<string, string[]> }
    | undefined

  const firstValidationError = data?.errors
    ? Object.values(data.errors).flat().at(0)
    : undefined

  return firstValidationError ?? data?.detail ?? data?.title ?? fallback
}

// FetchOptions<'json'>: varsayılan genel tip blob/text'i de kapsıyor ve raw<T> ile uyuşmuyor.
export async function apiFetch<T>(url: string, options: FetchOptions<'json'> = {}): Promise<T> {
  // Her çağrıda header yeniden kuruluyor; refresh sonrası yeni token'ı kullansın diye.
  const send = () => raw<T>(url, { ...options, headers: withAuthHeader(options.headers) })

  try {
    return await send()
  } catch (error) {
    if (!isUnauthorized(error)) {
      throw error
    }

    if (!(await refreshSession())) {
      clearSession()
      throw error
    }

    // Tek deneme; ikinci 401 olduğu gibi yukarı gider.
    return await send()
  }
}
