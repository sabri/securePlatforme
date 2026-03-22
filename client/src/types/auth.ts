// ─── Types matching the backend DTOs ─────────────────────────

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  firstName: string;
  lastName: string;
}

export interface RefreshTokenRequest {
  accessToken: string;
  refreshToken: string;
}

export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
}

export interface AuthResponse {
  succeeded: boolean;
  resultType: string;
  accessToken?: string;
  refreshToken?: string;
  expiresAt?: string;
  message?: string;
  user?: UserDto;
}
