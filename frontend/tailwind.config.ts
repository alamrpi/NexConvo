import type { Config } from 'tailwindcss';
import animate from 'tailwindcss-animate';

// All design tokens map to CSS variables defined in globals.css so light/dark
// theming flows through semantic tokens (frontend standard S25) and the spacing/
// radius scale stays on the 4px grid (S22). No raw hex lives here.
const config: Config = {
  darkMode: 'class',
  content: ['./src/**/*.{ts,tsx}'],
  theme: {
    container: {
      center: true,
      padding: '1.5rem',
      screens: {
        '2xl': '1280px',
      },
    },
    extend: {
      fontFamily: {
        sans: ['var(--font-sans)', 'var(--font-bengali)', 'system-ui', 'sans-serif'],
        bengali: ['var(--font-bengali)', 'var(--font-sans)', 'sans-serif'],
      },
      colors: {
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
        success: {
          DEFAULT: 'hsl(var(--success))',
          foreground: 'hsl(var(--success-foreground))',
        },
        warning: {
          DEFAULT: 'hsl(var(--warning))',
          foreground: 'hsl(var(--warning-foreground))',
        },
        chart: {
          1: 'hsl(var(--chart-1))',
          2: 'hsl(var(--chart-2))',
          3: 'hsl(var(--chart-3))',
          4: 'hsl(var(--chart-4))',
          5: 'hsl(var(--chart-5))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        popover: {
          DEFAULT: 'hsl(var(--popover))',
          foreground: 'hsl(var(--popover-foreground))',
        },
        card: {
          DEFAULT: 'hsl(var(--card))',
          foreground: 'hsl(var(--card-foreground))',
        },
        // Chat-specific tokens — brand colors (not themeable, same in light/dark)
        chatState: {
          ai: 'var(--chat-state-ai)',
          pending: 'var(--chat-state-pending)',
          human: 'var(--chat-state-human)',
          resolved: 'var(--chat-state-resolved)',
          closed: 'var(--chat-state-closed)',
        },
        chatChannel: {
          whatsapp: 'var(--chat-channel-whatsapp)',
          facebook: 'var(--chat-channel-facebook)',
          instagram: 'var(--chat-channel-instagram)',
          telegram: 'var(--chat-channel-telegram)',
          web: 'var(--chat-channel-web)',
        },
        chatConfidence: {
          high: 'var(--chat-confidence-high)',
          medium: 'var(--chat-confidence-medium)',
          low: 'var(--chat-confidence-low)',
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
      boxShadow: {
        // Slightly richer than shadow-sm, still subtle (S22). Used for interactive
        // cards on hover; shadow-sm remains the default elevation vocabulary.
        card: '0 1px 2px 0 hsl(255 40% 20% / 0.04), 0 2px 8px -2px hsl(255 40% 20% / 0.08)',
      },
      keyframes: {
        'accordion-down': {
          from: { height: '0' },
          to: { height: 'var(--radix-accordion-content-height)' },
        },
        'accordion-up': {
          from: { height: 'var(--radix-accordion-content-height)' },
          to: { height: '0' },
        },
      },
      animation: {
        'accordion-down': 'accordion-down 0.2s ease-out',
        'accordion-up': 'accordion-up 0.2s ease-out',
      },
    },
  },
  plugins: [animate],
};

export default config;
