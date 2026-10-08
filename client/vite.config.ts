import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const appHostPort = Number(process.env.PORT)
const apiProxyTarget = process.env.VITE_API_PROXY_TARGET ?? process.env.VITE_API_BASE_URL

export default defineConfig({
  plugins: [tailwindcss(), react()],
  server: {
    host: '0.0.0.0',
    port: Number.isInteger(appHostPort) ? appHostPort : 5173,
    strictPort: Boolean(process.env.PORT),
    // Requests arrive via Traefik/ngrok with a Host header Vite doesn't
    // recognize (e.g. *.ngrok-free.app); without this Vite rejects them
    // with "Blocked request. This host is not allowed".
    allowedHosts: true,
    proxy: apiProxyTarget
      ? {
          '/api': {
            target: apiProxyTarget,
            changeOrigin: true,
            secure: false,
          },
        }
      : undefined,
  },
})
