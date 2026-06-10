import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  if (mode === 'production' && !process.env.VITE_API_URL?.trim()) {
    throw new Error(
      'VITE_API_URL is required for production builds. Example: VITE_API_URL=https://api.example.com npm run build',
    );
  }

  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      host: true,
      open: 'http://localhost:5173',
      proxy: {
        '/api': {
          target: 'http://localhost:5188',
          changeOrigin: true,
        },
      },
    },
  };
});
