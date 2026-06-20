import 'server-only';

import { type NextRequest, NextResponse } from 'next/server';
import type { AxiosInstance } from 'axios';
import { readSession, writeSession, clearSession } from './session';
import { createServerApiClient, SessionExpiredError } from './api-server';
import type { AuthTokens } from '@/features/auth/model/auth.types';

type BffHandler = (req: NextRequest, ctx: { api: AxiosInstance }) => Promise<NextResponse>;

/**
 * Wraps an authenticated BFF route handler (frontend standard S4). It:
 *  - rejects with 401 if there is no session,
 *  - builds a request-scoped gateway client with the access token,
 *  - flushes any silently-rotated tokens back onto the response cookies,
 *  - maps a dead refresh token to 401 + cleared cookies.
 *
 * The handler receives a ready-to-use `api` client and must never touch cookies itself.
 */
export function withBff(handler: BffHandler) {
  return async (req: NextRequest): Promise<NextResponse> => {
    const session = await readSession();
    if (!session) {
      return NextResponse.json({ title: 'Unauthorized' }, { status: 401 });
    }

    let rotated: AuthTokens | null = null;
    const api = createServerApiClient(session, (tokens) => {
      rotated = tokens;
    });

    try {
      const res = await handler(req, { api });
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
