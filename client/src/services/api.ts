import axios from 'axios';
import type { LoginRequest, RegisterRequest, ForgotPasswordRequest, ResetPasswordRequest, AuthResponse, UserDto } from '../types/auth';

// ═══════════════════════════════════════════════════════════════
// [SECURITY: BFF PATTERN] — All API calls go to the same origin
// via the Vite proxy (/api/*). No external URLs are hardcoded.
// This ensures HTTP-only cookies are sent automatically and the
// backend URL is never leaked to client-side code.
// ═══════════════════════════════════════════════════════════════
const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true, // Required to send/receive cookies
});

// ═══════════════════════════════════════════════════════════════
// [SECURITY: CSRF / XSRF] — On every request, read the XSRF-TOKEN
// cookie (set by the server) and attach it as the X-XSRF-TOKEN
// header. This proves the request originates from our SPA, not
// from a malicious cross-site page (which cannot read our cookies
// due to SameSite=Strict).
// ═══════════════════════════════════════════════════════════════
api.interceptors.request.use((config) => {
  const csrfToken = getCookie('XSRF-TOKEN');
  if (csrfToken) {
    config.headers['X-XSRF-TOKEN'] = csrfToken;
  }
  return config;
});

// ═══════════════════════════════════════════════════════════════
// [SECURITY: HTTP-ONLY COOKIES] — When a 401 is received, attempt
// a silent token refresh via /api/auth/refresh. The server reads
// the RefreshToken from the HTTP-only cookie, rotates it, and
// sets new cookies. No tokens are ever stored in localStorage or
// accessible to JavaScript — preventing XSS-based token theft.
// ═══════════════════════════════════════════════════════════════
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        // Refresh uses cookies — no body needed
        const res = await axios.post<AuthResponse>('/api/auth/refresh', {}, {
          withCredentials: true,
        });

        if (res.data.succeeded) {
          return api(originalRequest);
        }
      } catch {
        // Refresh failed — redirect to login
        window.location.href = '/login';
      }
    }

    return Promise.reject(error);
  }
);

// ═══════════════════════════════════════════════════════════════
// [SECURITY: CSRF / XSRF] — Helper to parse a cookie value by
// name. Only non-HttpOnly cookies (like XSRF-TOKEN) are readable
// by JavaScript. The auth cookies (AccessToken, RefreshToken) are
// HttpOnly and invisible here — by design.
// ═══════════════════════════════════════════════════════════════
function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? decodeURIComponent(match[2]) : null;
}

// ─── Auth API calls ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════════════
// [SECURITY: BFF + HTTP-ONLY COOKIES] — No accessToken or
// refreshToken is ever passed in request bodies or stored in
// localStorage. The browser handles cookies automatically.
// ═══════════════════════════════════════════════════════════════
export const authApi = {
  // [SECURITY: CSRF] — Fetch initial CSRF token from server
  getCsrfToken: () =>
    api.get('/auth/csrf-token'),

  register: (data: RegisterRequest) =>
    api.post<AuthResponse>('/auth/register', data),

  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/auth/login', data),

  // Refresh uses cookies only — no body payload
  refresh: () =>
    api.post<AuthResponse>('/auth/refresh'),

  logout: () =>
    api.post('/auth/logout'),

  forgotPassword: (data: ForgotPasswordRequest) =>
    api.post<AuthResponse>('/auth/forgot-password', data),

  resetPassword: (data: ResetPasswordRequest) =>
    api.post<AuthResponse>('/auth/reset-password', data),

  getMe: () =>
    api.get<UserDto>('/auth/me'),
};

// ─── AI API calls ───────────────────────────────────────────
export const aiApi = {
  complete: (prompt: string) =>
    api.post<{ response: string }>('/ai/complete', { prompt }),

  ask: (prompt: string) =>
    api.post<{ response: string }>('/ai/ask', { prompt }),
};

export default api;
