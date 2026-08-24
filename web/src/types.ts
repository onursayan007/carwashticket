export type Role = 'Customer' | 'Staff' | 'Manager'

export interface UserDto {
  id: string
  email: string
  fullName: string | null
  roles: Role[]
}

export interface AuthResponse {
  accessToken: string
  expiresAt: string
  user: UserDto
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RegisterRequest {
  email: string
  password: string
  fullName?: string
}
