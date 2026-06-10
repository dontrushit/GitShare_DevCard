/** @type {import('tailwindcss').Config} */

export default {

  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],

  theme: {

    extend: {

      colors: {

        surface: 'var(--color-surface, #020617)',

        card: 'var(--color-card, #0f172a)',

        foreground: 'var(--color-foreground, #e2e8f0)',

        accent: 'var(--color-accent, #cbd5e1)',

        'accent-muted': 'var(--color-accent-muted, #94a3b8)',

        muted: 'var(--color-muted, #64748b)',

        ink: {

          border: 'var(--color-border, #1e293b)',

        },

      },

      boxShadow: {

        glow: '0 1px 0 0 rgba(148, 163, 184, 0.08)',

      },

    },

  },

  plugins: [],

};

