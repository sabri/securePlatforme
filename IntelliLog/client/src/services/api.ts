import axios from 'axios';
import type {
  LogInput, IngestLogsResult, GetLogsResult,
  IngestDocumentResult, GetDocumentsResult,
  SearchResult, ClassifyTextResult,
  DetectAnomaliesResult,
  RegisterWebhookResult, GetWebhooksResult,
  GenerateDataResult, TrainModelResult,
} from '../types';
import type { UserDto } from '../types/auth';

const SECUREPLATFORM_LOGIN = 'http://localhost:5173/login';

// ─── Axios instance (BFF pattern — same origin via Vite proxy) ───
const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

// Read the XSRF-TOKEN cookie (shared from SecurePlatform)
api.interceptors.request.use((config) => {
  const csrfToken = getCookie('XSRF-TOKEN');
  if (csrfToken) {
    config.headers['X-XSRF-TOKEN'] = csrfToken;
  }
  return config;
});

// On 401, redirect to SecurePlatform login with returnUrl
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      const returnUrl = encodeURIComponent(window.location.href);
      window.location.href = `${SECUREPLATFORM_LOGIN}?returnUrl=${returnUrl}`;
    }
    return Promise.reject(error);
  }
);

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? decodeURIComponent(match[2]) : null;
}

// ─── Auth API (proxied to SecurePlatform backend) ───────────
export const authApi = {
  getMe: () =>
    api.get<UserDto>('/auth/me'),

  getCsrfToken: () =>
    api.get('/auth/csrf-token'),
};

// ─── Logs API ───────────────────────────────────────────────
export const logsApi = {
  ingest: (logs: LogInput[]) =>
    api.post<IngestLogsResult>('/logs/ingest', { logs }),

  getAll: (params?: { severity?: string; source?: string; page?: number; pageSize?: number }) =>
    api.get<GetLogsResult>('/logs', { params }),

  detectAnomalies: (windowSize = 100) =>
    api.post<DetectAnomaliesResult>('/logs/detect-anomalies', null, { params: { windowSize } }),
};

// ─── Documents API ──────────────────────────────────────────
export const documentsApi = {
  ingest: (title: string, content: string) =>
    api.post<IngestDocumentResult>('/documents', { title, content }),

  getAll: (params?: { category?: string; page?: number; pageSize?: number }) =>
    api.get<GetDocumentsResult>('/documents', { params }),
};

// ─── Search / RAG API ───────────────────────────────────────
export const searchApi = {
  search: (q: string, topK = 5) =>
    api.get<SearchResult>('/search', { params: { q, topK } }),

  classify: (text: string, modelType: string) =>
    api.post<ClassifyTextResult>('/search/classify', { text, modelType }),
};

// ─── Webhooks API ───────────────────────────────────────────
export const webhooksApi = {
  register: (name: string, url: string, eventType: string) =>
    api.post<RegisterWebhookResult>('/webhooks', { name, url, eventType }),

  getAll: () =>
    api.get<GetWebhooksResult>('/webhooks'),
};

// ─── Data Generation & Training API ─────────────────────────
export const dataApi = {
  generate: (logs = 100, documents = 20) =>
    api.post<GenerateDataResult>('/data/generate', null, { params: { logs, documents } }),

  train: (modelType: string) =>
    api.post<TrainModelResult>('/data/train', { modelType }),
};

export default api;
