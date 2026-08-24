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
