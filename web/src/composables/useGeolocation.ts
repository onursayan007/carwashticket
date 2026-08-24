import { ref } from 'vue'

// Konum alınamazsa Antalya merkez. Harita boş açılmasın diye bir yere düşmesi gerekiyor.
export const FALLBACK_POSITION = { latitude: 36.8969, longitude: 30.7133 }

export type PermissionState = 'asking' | 'granted' | 'denied' | 'unavailable'

export function useGeolocation() {
  const latitude = ref(FALLBACK_POSITION.latitude)
  const longitude = ref(FALLBACK_POSITION.longitude)
  const state = ref<PermissionState>('asking')

  function locate(): Promise<void> {
    if (!navigator.geolocation) {
      state.value = 'unavailable'
      return Promise.resolve()
    }

    return new Promise((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => {
          latitude.value = position.coords.latitude
          longitude.value = position.coords.longitude
          state.value = 'granted'
          resolve()
        },
        () => {
          // Reddedildi veya zaman aşımı: fallback konumla devam ediyoruz.
          state.value = 'denied'
          resolve()
        },
        { enableHighAccuracy: true, timeout: 8000, maximumAge: 60_000 },
      )
    })
  }

  return { latitude, longitude, state, locate }
}
