import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

const backendValidationTarget = process.env.VITE_API_PROXY_TARGET || 'https://localhost:5001'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    minify: false,
    commonjsOptions: {
      include: [/zustand/, /node_modules/],
    },
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes('zustand')) {
            return 'zustand';
          }
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': backendValidationTarget,
      '/hubs': {
        target: backendValidationTarget,
        ws: true,
        secure: false,
      },
    },
  },
})
