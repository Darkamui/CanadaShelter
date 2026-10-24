import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';
import { VitePWA } from 'vite-plugin-pwa';

// Shelter.Host dev URL (backend/Shelter.Host/Properties/launchSettings.json).
const apiTarget = 'http://localhost:5080';

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'script', // plain registerSW.js in index.html; no workbox-window runtime dep
      manifest: {
        name: 'Gestion de refuge',
        short_name: 'Refuge',
        lang: 'fr-CA',
        start_url: '/',
        display: 'standalone',
        theme_color: '#1D4F48',
        background_color: '#F6F5F1',
        icons: [{ src: '/icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
      },
      workbox: {
        // App shell only. API responses are never cached (tenant data, permissions).
        // Fonts: the Latin subsets only (French and English); other scripts load on demand if ever used.
        globPatterns: ['**/*.{js,css,html,svg}', '**/*latin*.woff2'],
        navigateFallbackDenylist: [/^\/api\//, /^\/health\//],
        runtimeCaching: [],
      },
    }),
  ],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiTarget,
      '/health': apiTarget,
    },
  },
  preview: {
    port: 4173,
  },
});
