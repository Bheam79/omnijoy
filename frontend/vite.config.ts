import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { resolve } from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Live stream WebSocket ingest — must be listed before the generic /api
      // entry so Vite matches the more specific path first and enables ws: true.
      '/api/live': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
      // Proxy API calls to the .NET backend during dev
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      // Proxy SignalR hub connections during dev
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
      // Proxy share pages (OG/crawler HTML) to the .NET backend during dev.
      // ShareController lives at /share/* outside /api — without this proxy
      // Vite serves the SPA shell and crawler tests see no og: meta tags.
      '/share': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../backend/Omnijoy.Api/wwwroot',
    emptyOutDir: true,
  },
  test: {
    globals: true,
    environment: 'jsdom',
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov', 'html'],
      thresholds: {
        lines: 95,
        functions: 95,
        branches: 95,
        statements: 95,
      },
    },
  },
})
