import type { components } from '@/api/types'

type Schemas = components['schemas']

// Backend'de Identity rolü düz string; uygulamada dar tutuyoruz.
export type Role = 'Customer' | 'Staff' | 'Manager'

export type LoginRequest = Schemas['LoginRequest']
export type RegisterRequest = Schemas['RegisterRequest']

// Swashbuckle yanıt alanlarını optional üretiyor ama sunucu hepsini her zaman
// gönderiyor; Required ile daraltıp kullanım tarafında gereksiz kontrolden kurtuluyoruz.
export type UserDto = Omit<Required<Schemas['UserDto']>, 'roles'> & { roles: Role[] }

export type AuthResponse = Omit<Required<Schemas['AuthResponse']>, 'user'> & { user: UserDto }

export type ServiceDto = Required<Schemas['ServiceDto']>

export type StationListItemDto = Required<Schemas['StationListItemDto']>

export type StationDetailDto = Omit<Required<Schemas['StationDetailDto']>, 'services'> & {
  services: ServiceDto[]
}

export type OrderStatus = Schemas['OrderStatus']

export type CreateOrderRequest = Required<Schemas['CreateOrderRequest']>

// redirectUrl sağlayıcı adresini taşır, null olabilir.
export type CreateOrderResponse = Omit<Required<Schemas['CreateOrderResponse']>, 'redirectUrl'> & {
  redirectUrl: string | null
}

export type OrderStatusResponse = Required<Schemas['OrderStatusResponse']>
