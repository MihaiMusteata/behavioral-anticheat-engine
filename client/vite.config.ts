import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const appHostPort = Number(process.env.PORT)
const apiBaseUrl = process.env.VITE_API_BASE_URL

export default defineConfig({
  plugins: [tailwindcss(), react()],
  server: {
    host: '0.0.0.0',
    port: Number.isInteger(appHostPort) ? appHostPort : 5173,
    strictPort: Boolean(process.env.PORT),
    proxy: apiBaseUrl
      ? {
          '/api': {
            target: apiBaseUrl,
            changeOrigin: true,
            secure: false,
          },
        }
      : undefined,
  },
})
