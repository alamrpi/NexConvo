import 'server-only';

import { type NextRequest, NextResponse } from 'next/server';
import type { AxiosInstance } from 'axios';
import { readSession, writeSession, clearSession } from './session';
import { createServerApiClient, SessionExpiredError } from './api-server';
import { resolveCorrelationId } from './correlation';
import type { AuthTokens } from '@/features/auth/model/auth.types';

/** Next.js route context (e.g. `{ params: Promise<{ id: string }> }` for dynamic segments). */
type RouteContext = { params: Promise<Record<string, string>> };
type BffHandler = (
  req: NextRequest,
  ctx: { api: AxiosInstance },
  routeCtx: RouteContext,
) => Promise<NextResponse>;

/**
 * Wraps an authenticated BFF route handler (frontend standard S4). It:
 *  - rejects with 401 if there is no session,
 *  - builds a request-scoped gateway client with the access token,
 *  - flushes any silently-rotated tokens back onto the response cookies,
 *  - maps a dead refresh token to 401 + cleared cookies.
 *
 * The handler receives a ready-to-use `api` client and the Next route context (for dynamic
 * `[id]` segments), and must never touch cookies itself.
 *
 * Pass `e2eFixture` to short-circuit in E2E mode (NEXT_PUBLIC_E2E=true) — returns
 * deterministic data without a real session or backend. Never active in production.
 */
export function withBff(
  handler: BffHandler,
  e2eFixture?: (req: NextRequest, routeCtx: RouteContext) => NextResponse,
) {
  return async (req: NextRequest, routeCtx: RouteContext): Promise<NextResponse> => {
    if (process.env.NEXT_PUBLIC_E2E === 'true' && e2eFixture) {
      return e2eFixture(req, routeCtx);
    }

    const session = await readSession();
    if (!session) {
      return NextResponse.json({ title: 'Unauthorized' }, { status: 401 });
    }

    let rotated: AuthTokens | null = null;
    const api = createServerApiClient(
      session,
      (tokens) => {
        rotated = tokens;
      },
      resolveCorrelationId(req),
    );

    try {
      const res = await handler(req, { api }, routeCtx);
      if (rotated) {
        writeSession(res, rotated);
      }
      return res;
    } catch (error) {
      if (error instanceof SessionExpiredError) {
        const res = NextResponse.json({ title: 'Session expired' }, { status: 401 });
        clearSession(res);
        return res;
      }
      throw error;
    }
  };
}
