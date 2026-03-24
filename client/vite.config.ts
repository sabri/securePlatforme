import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// ═══════════════════════════════════════════════════════════════
// [SECURITY: BFF PATTERN] — Vite dev server proxies all /api/*
// requests to the .NET backend. In production, NGINX/reverse
// proxy would do the same. This ensures:
//   1. Same-origin policy → cookies sent automatically
//   2. No CORS issues (everything is same-origin)
//   3. Backend URL never exposed to the client
// ═══════════════════════════════════════════════════════════════
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7220',
        changeOrigin: true,
        secure: false, // Accept self-signed certs in development
      },
    },
  },
})
