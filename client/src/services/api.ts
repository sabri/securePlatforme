import axios from 'axios';
import type { LoginRequest, RegisterRequest, RefreshTokenRequest, AuthResponse, UserDto } from '../types/auth';

const API_BASE = 'http://localhost:5000/api';

const api = axios.create({
  baseURL: API_BASE,
  headers: { 'Content-Type': 'application/json' },
});

// ─── Interceptor: Auto-attach JWT token ─────────────────────
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// ─── Interceptor: Auto-refresh on 401 ──────────────────────
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      const accessToken = localStorage.getItem('accessToken');
      const refreshToken = localStorage.getItem('refreshToken');

      if (accessToken && refreshToken) {
        try {
          const res = await axios.post<AuthResponse>(`${API_BASE}/auth/refresh`, {
            accessToken,
            refreshToken,
          });

          if (res.data.succeeded && res.data.accessToken) {
            localStorage.setItem('accessToken', res.data.accessToken);
            localStorage.setItem('refreshToken', res.data.refreshToken!);
            originalRequest.headers.Authorization = `Bearer ${res.data.accessToken}`;
            return api(originalRequest);
          }
        } catch {
          // Refresh failed — force logout
          localStorage.clear();
          window.location.href = '/login';
        }
      }
    }

    return Promise.reject(error);
  }
);

// ─── Auth API calls ─────────────────────────────────────────
export const authApi = {
  register: (data: RegisterRequest) =>
    api.post<AuthResponse>('/auth/register', data),

  login: (data: LoginRequest) =>
    api.post<AuthResponse>('/auth/login', data),

  refresh: (data: RefreshTokenRequest) =>
    api.post<AuthResponse>('/auth/refresh', data),

  logout: () =>
    api.post('/auth/logout'),

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
