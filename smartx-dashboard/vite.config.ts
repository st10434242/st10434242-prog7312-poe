import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const gateway = 'http://localhost:5225'

// Proxying keeps the dev server same-origin with the gateway, so CORS never comes into it
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': { target: gateway, changeOrigin: true },
      // ws so the SignalR connection can upgrade instead of falling back to long polling
      '/hub': { target: gateway, changeOrigin: true, ws: true },
    },
  },
})
