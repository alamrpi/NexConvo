# NexConvo Frontend — Authentication

Core auth is implemented as a **Backend-For-Frontend (BFF)**: the browser never holds a
token. All gateway traffic flows **browser → same-origin `/api/bff/*` → YARP gateway →
Identity service**, and access/refresh tokens live only in `HttpOnly; SameSite=Strict`
cookies set by the BFF route handlers.

## Run it locally

Ports are fixed: **frontend 3003**, **gateway 5055**, **Identity 5047** (so they don't
clash with other local projects on 3000/3001).

```powershell
# 1. Infra (Postgres/Redis/RabbitMQ/Seq) — from repo root
docker compose up -d

# 2. Identity DB migrations (only needed once / after schema changes)
dotnet ef database update `
  --project src/services/Identity/NexConvo.Identity.Infrastructure `
  --startup-project src/services/Identity/NexConvo.Identity.Api

# 3. Identity service  → http://localhost:5047
dotnet run --project src/services/Identity/NexConvo.Identity.Api --launch-profile http

# 4. Gateway           → http://localhost:5055  (routes /api/v1/auth/* to Identity in dev)
dotnet run --project src/gateway/NexConvo.Gateway --launch-profile http

# 5. Frontend          → http://localhost:3003
cd frontend; npm run dev
```

`frontend/.env.local` points the BFF at the gateway: `API_GATEWAY_URL=http://localhost:5055`.

Open http://localhost:3003 → **Sign up** creates a workspace (tenant) + owner and logs you
straight into the dashboard; **Log in** uses workspace slug + email + password.

## Verified end-to-end (2026-06-21, via the gateway)

| Flow | Result |
|---|---|
| Signup (`POST /api/bff/auth/signup`) | 201, sets cookies, no token in body; duplicate slug → 409 |
| Login (`POST /api/bff/auth/login`) | 200, **HttpOnly + SameSite=Strict** cookies, no token in body |
| `/me` (`GET /api/bff/auth/me`) | 200 with session; 401 without cookies |
| Bad credentials | 401 (surfaced as an inline form error) |
| Logout (`POST /api/bff/auth/logout`) | 204, cookies cleared, subsequent `/me` → 401 |
| Dashboard guard | unauth `/dashboard` → 307 `/login`; authed → 200 |

`Secure` on cookies is intentionally **off in development** (so `http://localhost` works)
and **on in production** — see `src/shared/api/server/session.ts`.

## Unit-verified (not runtime-exercised)

- **Silent access-token refresh** (single-flight on 401, rotation, `SessionExpiredError`)
  is covered by `src/shared/api/server/api-server.test.ts`. Exercising it live requires the
  15-min access token to expire (or lowering `Jwt:AccessTokenMinutes`). The full-page-load
  refresh path lives in `src/middleware.ts`.

## Not yet built

- **Signup is the only onboarding path; password reset / "forgot password" is a stub
  (`#forgot`).**
- Browser-DevTools manual pass (cookie inspection, visual states) — automated HTTP checks
  cover the contract, but a human visual pass is still worth doing.
