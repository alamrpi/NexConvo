# NexConvo Web

The Next.js 15 (App Router) frontend for NexConvo — built to the project's
`nexconvo-frontend-standards` (Feature-Sliced layering, strict TS, RSC-first, BFF-ready,
Bengali-first i18n, WCAG AA, and the UI/UX design standards S22–S28).

## Stack

- **Next.js 15** App Router + React 19 (server components by default)
- **Tailwind CSS** + **shadcn/ui** (Radix primitives) — semantic design tokens, light/dark
- **lucide-react** icons · **framer-motion** (subtle, reduced-motion-aware entrances)
- **next-intl** (cookie locale, `en` + `bn`, no URL prefix) · **next-themes**
- **react-hook-form** + **zod** forms · **Vitest** + Testing Library

## Getting started

```bash
cp .env.example .env.local   # set API_GATEWAY_URL (server-only)
npm install
npm run dev                  # http://localhost:3000
```

## Scripts

| Script | Purpose |
|---|---|
| `npm run dev` | Dev server |
| `npm run build` / `npm start` | Production build / serve |
| `npm run lint` | ESLint (bans `any` / non-null `!`) |
| `npm run typecheck` | `tsc --noEmit` (strict) |
| `npm run test` | Vitest |

## Structure (Feature-Sliced)

```
src/
├── app/                      # routes: (marketing) /, (auth) /login, (dashboard) /dashboard
├── shared/                   # ui/ (design system), lib/ (cn, env), config/ (nav), i18n/
└── features/                 # marketing, auth, dashboard, theming — each owns its components
```

Routes are split by group: `/` is the landing page, `/login` the split-screen auth, and the
dashboard shell wraps `/dashboard`. Tokens/secrets and data fetching (BFF, React Query) are not
yet wired — the structure reserves their homes for when the Identity/CRM services are connected.
