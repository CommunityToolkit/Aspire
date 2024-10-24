import { defineConfig } from 'vite'
import deno from '@deno/vite-plugin'
import react from '@vitejs/plugin-react'

const port = parseInt(Deno.env.get('PORT') || '3000');

// https://vite.dev/config/
export default defineConfig({
  plugins: [deno(), react()],
  server: {
    port: port
  }
})
