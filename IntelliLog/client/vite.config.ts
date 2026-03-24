import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    proxy: {
      // Auth requests → SecurePlatform API (shared JWT cookies)
      '/api/auth': {
        target: 'https://localhost:7220',
        changeOrigin: true,
        secure: false,
      },
      // Business API → IntelliLog API
      '/api': {
        target: 'http://localhost:5030',
        changeOrigin: true,
        secure: false,
      },
      '/hubs': {
        target: 'http://localhost:5030',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
