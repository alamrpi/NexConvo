---
name: nexconvo-frontend-standards
description: Use when implementing, designing, scaffolding, or reviewing any NexConvo frontend code — a page, component, hook, API call, form, route handler (BFF), auth/session logic, state, or UI/UX/visual design — especially under time pressure, for a demo, or when a change feels "too simple" to need the full pattern. Enforces the non-negotiable frontend engineering standards (Feature-Sliced layering, strict TypeScript, BFF/HttpOnly token security, single resilient API client with silent refresh, server/client component discipline, server-vs-client state separation, tenant isolation, authorization-aware UI, accessibility, Bengali-first i18n, no client secrets, correlated observability, resilient async UX, bounded rendering, optimistic-concurrency handling, test-first logic) AND the UI/UX design standards (design tokens & spacing/elevation, typography hierarchy, mobile-first responsive layout, theming & dark-mode parity, motion discipline, visual states & feedback, clean B2B restraint & consistency) so they never have to be restated per prompt.
---

# NexConvo Frontend Engineering Standards

## Overview

The NexConvo frontend is the client of an enterprise, multi-tenant SaaS CRM. Its standards are **architectural and security guarantees, not style preferences**. A token in `localStorage`, a `fetch` inside a component, an `any` that erases a backend contract, or a Delete button "secured" only by hiding it is not "a faster version of the same thing" — it is a different, non-compliant system that leaks tokens to XSS, scatters un-refreshable network calls, desyncs from the backend, or ships a privilege-escalation hole.

**The core principle: violating the letter of these standards is violating the spirit of them.** "I followed the spirit, just faster" is the exact rationalization this skill exists to stop. The frontend pairs with the backend `nexconvo-enterprise-standards` skill — this is the client-side enforcement layer.

## When to Use

- Scaffolding the Next.js app or adding any page, layout, component, hook, store, schema, or BFF route handler
- Wiring authentication, sessions, tokens, or tenant routing
- Calling the API gateway from the frontend (server or client)
- Reviewing a frontend diff or design
- **Any time you catch yourself thinking "this one is too small/urgent to need the full pattern"** — that thought is the trigger, not the exemption

## The Non-Negotiable Standards

Every one of these applies to *every* change, including a one-field form or a demo screen.

| # | Standard | Concretely means |
|---|---|---|
| 1 | **Feature-Sliced layering** | UI (`components`) / logic (`hooks` in `api`) / data + model (`types`, `schema`, `store`) are separated. Components NEVER call `fetch`/`axios` directly and hold no business logic — they consume hooks. Features expose a narrow public surface; no deep cross-feature imports. |
| 2 | **Strict TypeScript, zero `any`** | No `any`, no `@ts-ignore`, no non-null `!` to silence the compiler. Cross boundaries with `unknown` + narrowing. Frontend DTO types **mirror the backend records exactly**; runtime boundaries (BFF input, env) are zod-validated and types are `z.infer`red. |
| 3 | **BFF / HttpOnly token security** | Access + refresh tokens live ONLY in `HttpOnly; Secure; SameSite=Strict` cookies set by Next.js route handlers. NEVER `localStorage`, `sessionStorage`, a JS-readable cookie, Zustand, Redux, or a React state. Browser code never reads a token. All gateway traffic flows browser → same-origin `/api/bff/*` → gateway. |
| 4 | **Single resilient API client** | One configured client per side (browser → `/api/bff`; server → gateway). The server client does **silent single-flight refresh** on 401 and retries. No ad-hoc `fetch` scattered through the app; every external call lives in the data layer. |
| 5 | **SOLID & Clean Code** | Small, single-responsibility components and hooks — no God component/hook. Composition over giant trees. Dependencies via props/context, not reaching into module singletons. Intention-revealing names, guard clauses over deep nesting, no dead/commented-out code. |
| 6 | **Server-first components** | React Server Components by default. `'use client'` only where interactivity/hooks require it, pushed to leaf components. Data fetching and anything secret stays server-side. A secret or token value NEVER reaches the client bundle. |
| 7 | **Server vs client state separation** | Server state (anything from the API) is owned by React Query — no manual caching, no copying it into a global store. Client/session/UI state uses Zustand. Never duplicate or desync server data into a global store. |
| 8 | **Tenant isolation** | The tenant slug is resolved through ONE helper (single source of truth). Protected layouts guard tenant access. The tenant is never trusted from editable client input for an authorization decision — the server enforces it via RLS/JWT. |
| 9 | **Authorization-aware UI (deny-by-default)** | Routes and actions are gated by the session's roles/permissions (honoring the `*` wildcard), deny-by-default. Hiding UI is UX only — it is **never** the security boundary; the server still authorizes every call. |
| 10 | **Forms & validation** | React Hook Form + zod. The zod schema is the single validation source, shared with the BFF route handler. Client validation is UX, never the security boundary — the backend validates too. |
| 11 | **Accessibility (WCAG 2.1 AA)** | Semantic HTML, labeled inputs, full keyboard operability, managed focus, sufficient contrast. Interactive elements are real `<button>`/`<a>`, not click-handlered `<div>`s. |
| 12 | **Bengali-first i18n** | No hardcoded user-facing strings — all copy flows through the i18n layer. Locale-aware number/date formatting. Bengali is a **first-class** locale (per CLAUDE.md), not an afterthought; layouts tolerate long translated strings. |
| 13 | **Secrets & config** | Only non-sensitive values use `NEXT_PUBLIC_`. Gateway URL and any key stay server-only. Env is zod-validated at startup. Never inline a base URL or secret in client code. |
| 14 | **Observability & correlation** | Forward `x-correlation-id` / W3C `traceparent` through the BFF to the gateway so one trace spans browser → BFF → service. Each route segment has an error boundary. Structured client error reporting. **Never log PII** (email/phone/name) or any token. |
| 15 | **Resilient async UX** | Every async surface has explicit loading, error, and empty states. Mutations handle failure (inline/toast). No unhandled promise rejections. Transient failures retry with backoff (handled in the data layer). |
| 16 | **Bounded rendering & performance** | Large lists paginate or virtualize (mirror the backend page cap). Code-split by route; use Suspense boundaries; optimize images via `next/image`. Memoize only where measured — no premature optimization. |
| 17 | **Optimistic concurrency** | Surface backend `409 Conflict` to the user (reload/merge prompt) and send the expected version on edits. Never silently last-writer-wins in the UI. |
| 18 | **Reusable design system** | Shared, accessible UI primitives live in `shared/ui` — no duplicated ad-hoc buttons/inputs. Spacing/color come from Tailwind tokens, not magic values. |
| 19 | **API versioning & contract fidelity** | Consume only versioned `/api/v1/...` through the BFF. Frontend DTO types mirror backend contracts exactly and tolerate additive change. Never hand-parse untyped JSON into `any`. |
| 20 | **Test-first for logic** | Hooks, stores, zod schemas, and the refresh interceptor get a failing test BEFORE the implementation (red → green → refactor). Mock the network with MSW. No logic ships without its test. |
| 21 | **XSS hardening** | Never `dangerouslySetInnerHTML` without sanitization. Treat all server/user content as untrusted and rely on React's escaping. Keep code CSP-friendly. |
| 22 | **Design tokens, spacing & elevation** | Spacing, sizing, radius, shadow, and color come from the Tailwind token scale on a **4px grid** — never magic pixel values (`p-[13px]`, `mt-[7px]`) or raw hex/`rgb()`. Default elevation is a subtle `border` + `shadow-sm`; reserve heavier shadows for true overlays (modals, popovers). One radius/shadow vocabulary across the app. |
| 23 | **Typography hierarchy** | A single, defined type scale drives hierarchy: primary headings `text-3xl font-bold tracking-tight`, secondary/supporting copy `text-muted-foreground`. Readable measure (`max-w-prose`/~65ch) and leading for body text. Inter for UI, **Noto Sans Bengali** for `bn` — wired via font CSS variables, never ad-hoc `font-size`/`font-family`. |
| 24 | **Mobile-first responsive** | Author the **base (unprefixed) styles for mobile**, then layer `sm/md/lg/xl` — never desktop-first with `lg:`-overrides walking back to mobile. `flex-col` → `md:flex-row`/`lg:grid`; fluid widths and `min-w-0`, not fixed pixel columns that overflow. Touch targets ≥44px. Every screen verified at 360 / 768 / 1280. |
| 25 | **Theming & dark-mode parity** | Color flows through **semantic tokens** (`bg-background`, `text-foreground`, `bg-card`, `border-border`, `text-muted-foreground`, `bg-primary`) — never raw `bg-white`/`text-black`/hex that silently breaks dark mode. Every surface is built and checked in **both** themes; contrast meets WCAG AA (ties to S11) in each. |
| 26 | **Motion discipline** | Animation is subtle, purposeful, and fast (**≤300ms**), animating `transform`/`opacity` only (60fps — never `width`/`top`/`left` in a loop). **Honor `prefers-reduced-motion`** (framer-motion `useReducedMotion` / CSS media query). framer-motion is for entrances and view transitions, not decorative perpetual motion or attention-grabbing jank. |
| 27 | **Visual states & feedback** | The visual side of S15: loading uses **skeletons that match the final layout** (no layout shift / CLS), not a bare spinner on a blank page; empty states give guidance + a primary action; error states are human and actionable. Every interactive element has visible **hover / active / `focus-visible` / disabled** states; destructive actions are visually distinct (e.g. `variant="destructive"`) and confirmed. |
| 28 | **Clean B2B restraint & consistency** | The aesthetic baseline is Vercel/Linear/Stripe: generous whitespace, subtle borders, soft shadows, and a **restrained accent** (the indigo primary used sparingly for emphasis, not everywhere). Consistent density, alignment to a grid, and a **single icon set (lucide-react)** at consistent size/stroke. No gratuitous gradients, rainbow palettes, or one-off bespoke components when a `shared/ui` primitive exists (ties to S18). |

## Red Flags — STOP, you are about to violate a standard

If you think or write any of these, stop and follow the standard instead:

- "I'll stash the token in `localStorage` so the Authorization header is easy" → **HttpOnly-cookie via the BFF only (Standard 3)**
- "This component can just `fetch` directly, it's one call" → **data layer + hook only (Standards 1, 4)**
- "`as any` / `@ts-ignore` to make the type error go away" → **no `any`; narrow `unknown` (Standard 2)**
- "I'll copy the user / the list into Zustand so it's global" → **server state belongs to React Query (Standard 7)**
- "It's an internal screen, skip the label/keyboard handling" → **WCAG AA is non-negotiable (Standard 11)**
- "Hardcode the English string now, translate later" → **i18n from line one; Bengali is first-class (Standard 12)**
- "Hiding the Delete button for non-admins IS the authorization" → **UI gating is UX; the server authorizes (Standard 9)**
- "Put the gateway URL/key in `NEXT_PUBLIC_` so the client can read it" → **secrets/config server-only (Standard 13)**
- "Just render all the rows, the list is small" → **paginate/virtualize (Standard 16)**
- "Last-writer-wins is fine in the UI" → **surface the 409 conflict (Standard 17)**
- "`'use client'` at the top of the page is simpler" → **RSC by default, client at the leaves (Standard 6)**
- "I'll add the hook/store test after it works" → **test-first for logic (Standard 20)**
- "I'll interpolate the server HTML with `dangerouslySetInnerHTML`" → **sanitize or don't (Standard 21)**
- "`p-[13px]` / `#4f46e5` to nudge this pixel-perfect" → **token scale on a 4px grid, no magic values (Standard 22)**
- "A big drop shadow makes this card pop" → **subtle `border` + `shadow-sm` is the default elevation (Standard 22)**
- "I'll size the heading with a one-off `text-[28px]`" → **use the defined type scale (Standard 23)**
- "I'll start from the desktop layout and add `lg:flex-col` for mobile" → **mobile-first base, layer up (Standard 24)**
- "`bg-white` / `text-black` is fine here" → **semantic tokens; raw colors break dark mode (Standard 25)**
- "I'll get it looking right in light mode and check dark later" → **build and verify both themes together (Standard 25)**
- "A slick 600ms animated entrance will look premium" → **≤300ms, transform/opacity, respect reduced-motion (Standard 26)**
- "A spinner on a blank page is fine while it loads" → **skeleton matching the layout, no CLS (Standard 27)**
- "The button doesn't need a distinct disabled/focus look" → **hover/active/focus-visible/disabled are required (Standard 27)**
- "I'll grab an icon from another set / hand-roll this button" → **single lucide set; reuse `shared/ui` (Standards 28, 18)**

## Rationalizations and Reality

| Rationalization | Reality |
|---|---|
| "A token in memory/`localStorage` is fine if it's short-lived." | Any JS-readable store is exfiltratable by one XSS payload. The `HttpOnly` cookie via the BFF is the same effort and immune — that's the whole point of the BFF. |
| "One direct `fetch` in a component is harmless." | It bypasses silent refresh, correlation IDs, and error handling, and it metastasizes across the codebase. The hook is the identical call placed in the right layer. |
| "`any` unblocks me now." | It deletes the contract the rest of the app relies on and the compiler can no longer protect you. One `unknown` + guard keeps the boundary intact. |
| "Bengali/i18n can come later." | Retrofitting i18n means re-touching every string in the app. It's a first-class requirement in CLAUDE.md — wire it from the first string. |
| "Hiding the button is enough security." | The endpoint is still callable with a crafted request. Client gating is cosmetic; the deny-by-default authorization lives on the server. |
| "React Query AND Zustand for the same data is convenient." | Two sources of truth desync the instant one updates. Server data has exactly one owner: React Query. |
| "Skipping the loading/error state is fine for the demo." | The demo is exactly where a spinnerless blank screen or an unhandled rejection shows. The three states are part of the feature, not polish. |
| "A magic `p-[13px]` is faster than finding the right token." | One off-grid value invites the next, and the UI drifts into visual inconsistency no review can catch line-by-line. The token (`p-3`/`p-4`) is the same keystroke count and keeps the system coherent. |
| "Raw `bg-white` is fine, we'll do dark mode later." | "Later" means re-auditing every surface in the app for hardcoded colors. The semantic token (`bg-background`/`bg-card`) is the same length and works in both themes from line one. |
| "A longer, fancier animation feels more premium." | Slow or perpetual motion reads as sluggish, blocks interaction, and triggers motion sickness for some users. Premium is fast and subtle (≤300ms, transform/opacity) and respects `prefers-reduced-motion`. |
| "A spinner is enough; the skeleton is polish." | A spinner on a blank page hides the layout and shifts content when data lands (CLS). The skeleton that mirrors the final layout is the loading state, not decoration — it's part of the feature. |
| "One bespoke button here won't hurt." | Every one-off button is a future inconsistency and an un-themed, un-accessible surface. The `shared/ui` primitive is already styled, tokenized, and keyboard-accessible — reuse it. |
| "I'm following the spirit, just pragmatically." | Violating the letter IS violating the spirit. The standards are the spirit, expressed precisely. |

## Correct Pattern (a "trivial" gated read, done right)

A "simple" lead-count widget still flows through every layer — typed contract, data layer, React Query hook, and a permission-gated component with all three async states:

```ts
// features/leads/model/lead.types.ts — mirrors the backend DTO (S2, S19)
export interface LeadSummary { total: number; newThisWeek: number; }

// features/leads/api/use-lead-summary.ts — data layer + server-state hook (S1, S4, S7)
import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/client/api-client';
import type { LeadSummary } from '../model/lead.types';

export function useLeadSummary() {
  return useQuery({
    queryKey: ['leads', 'summary'],
    queryFn: async (): Promise<LeadSummary> => {
      const { data } = await apiClient.get<LeadSummary>('/crm/leads/summary'); // → BFF → gateway
      return data;
    },
  });
}

// features/leads/components/lead-summary-card.tsx — UI only, gated, all states (S5, S9, S11, S15)
'use client';
import { useSessionStore } from '@/features/auth/model/session.store';
import { useLeadSummary } from '../api/use-lead-summary';

export function LeadSummaryCard() {
  const canRead = useSessionStore((s) => s.hasPermission('leads:read'));
  const { data, isLoading, isError } = useLeadSummary();

  if (!canRead) return null;                              // UX gate (server still authorizes — S9)
  if (isLoading) return <p role="status">Loading…</p>;   // i18n key in real code (S12)
  if (isError) return <p role="alert">Couldn’t load leads.</p>;
  if (!data) return <p>No leads yet.</p>;

  return (
    <section aria-label="Lead summary">
      <p>{data.total} total · {data.newThisWeek} new this week</p>
    </section>
  );
}
```

And the test that exists **before** the hook does (S20):

```ts
// features/leads/api/use-lead-summary.test.ts — MSW-mocked, red first
it('returns the lead summary from the BFF', async () => {
  server.use(http.get('/api/bff/crm/leads/summary',
    () => HttpResponse.json({ total: 12, newThisWeek: 3 })));
  const { result } = renderHook(() => useLeadSummary(), { wrapper });
  await waitFor(() => expect(result.current.data).toEqual({ total: 12, newThisWeek: 3 }));
});
```

## Common Mistakes

- Treating "demo" or "urgent" as a standards exemption — it never is; the compliant pattern is barely slower.
- Storing a token anywhere JS can read it instead of an `HttpOnly` cookie via the BFF.
- `fetch`/`axios` calls inside components instead of a data-layer hook.
- `any` / `@ts-ignore` that erases a backend contract instead of `unknown` + narrowing.
- Mirroring server data into Zustand/Redux instead of letting React Query own it.
- Treating a hidden button as authorization instead of a server-enforced policy.
- Hardcoded English strings instead of i18n keys (Bengali is first-class).
- Missing loading/error/empty states, or an unhandled mutation rejection.
- `NEXT_PUBLIC_` on a real secret, or a gateway URL hardcoded in client code.
- Unbounded list rendering with no pagination/virtualization.
- Silent last-writer-wins instead of surfacing a 409 conflict.
- Marking a hook/store/schema done with no failing-test-first history.
- Magic pixel values (`p-[13px]`) or raw hex/`rgb()` instead of the 4px token scale (S22).
- Heavy drop shadows as the default card elevation instead of subtle `border` + `shadow-sm` (S22).
- One-off `text-[28px]`/ad-hoc font sizes instead of the defined type scale (S23).
- Desktop-first markup walked back to mobile with `lg:`-overrides instead of mobile-first base styles (S24).
- Hardcoded `bg-white`/`text-black`/hex that breaks dark mode instead of semantic tokens (S25).
- Shipping a screen verified only in light mode (S25).
- Animations >300ms, animating layout properties, or ignoring `prefers-reduced-motion` (S26).
- A bare spinner on a blank page (with CLS when data lands) instead of a layout-matching skeleton (S27).
- Interactive elements missing visible hover/active/`focus-visible`/disabled states (S27).
- Mixed icon sets or hand-rolled buttons/inputs instead of one lucide set + `shared/ui` primitives (S28, S18).