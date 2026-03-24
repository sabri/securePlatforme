// ─── Types matching the backend DTOs ─────────────────────────

// ═══════════════════════════════════════════════════════════════
// [SECURITY: HTTP-ONLY COOKIES] — RefreshTokenRequest is no
// longer needed on the client because refresh tokens are stored
// in HTTP-only cookies and sent automatically by the browser.
// The server reads them directly from the cookie.
// ═══════════════════════════════════════════════════════════════

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

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  code: string;
  newPassword: string;
  confirmPassword: string;
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
