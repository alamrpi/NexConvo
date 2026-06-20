import { type NextRequest, NextResponse } from 'next/server';

/**
 * Route guard + full-page silent refresh (frontend standards S3, S9).
 *
 * Protected pages need a valid access token before the server layout fetches /me, but a
 * React Server Component cannot write cookies — so the ONE place full-page navigations
 * can refresh is here. On a /dashboard request:
 *   - no refresh token            → redirect to /login
 *   - access token valid          → continue
 *   - access expired, refresh ok  → rotate cookies, redirect to self (next request is fresh)
 *   - refresh fails               → clear cookies, redirect to /login
 *
 * Client-side data calls refresh through the BFF (withBff), so this only covers page loads.
 */

const ACCESS_COOKIE = 'nx_at';
const REFRESH_COOKIE = 'nx_rt';
const REFRESH_MAX_AGE_SECONDS = 60 * 60 * 24 * 14;
const EXPIRY_LEEWAY_MS = 10_000;

const cookieOptions = {
  httpOnly: true,
  secure: process.env.NODE_ENV !== 'development',
  sameSite: 'strict' as const,
  path: '/',
};

interface RefreshedTokens {
  accessToken: string;
  expiresInSeconds: number;
  refreshToken: string;
}

export async function middleware(req: NextRequest): Promise<NextResponse> {
  const refreshToken = req.cookies.get(REFRESH_COOKIE)?.value;
  if (!refreshToken) {
    return redirectToLogin(req);
  }

  const accessToken = req.cookies.get(ACCESS_COOKIE)?.value;
  if (accessToken && !isExpired(accessToken)) {
    return NextResponse.next();
  }

  const rotated = await tryRefresh(refreshToken);
  if (!rotated) {
    const res = redirectToLogin(req);
    clearAuthCookies(res);
    return res;
  }

  // Set the fresh cookies and bounce to the same URL so the next request (and its RSC
  // layout) reads the rotated access token. No request-header rewriting needed.
  const res = NextResponse.redirect(req.nextUrl);
  res.cookies.set(ACCESS_COOKIE, rotated.accessToken, {
    ...cookieOptions,
    maxAge: rotated.expiresInSeconds,
  });
  res.cookies.set(REFRESH_COOKIE, rotated.refreshToken, {
    ...cookieOptions,
    maxAge: REFRESH_MAX_AGE_SECONDS,
  });
  return res;
}

function redirectToLogin(req: NextRequest): NextResponse {
  const url = req.nextUrl.clone();
  url.pathname = '/login';
  url.search = '';
  return NextResponse.redirect(url);
}

function clearAuthCookies(res: NextResponse): void {
  res.cookies.set(ACCESS_COOKIE, '', { ...cookieOptions, maxAge: 0 });
  res.cookies.set(REFRESH_COOKIE, '', { ...cookieOptions, maxAge: 0 });
}

/** Read the JWT `exp` without verifying (edge-safe) — verification happens at the gateway. */
function isExpired(token: string): boolean {
  try {
    const payload = token.split('.')[1];
    if (!payload) return true;
    const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/'))) as {
      exp?: number;
    };
    return typeof json.exp !== 'number' || json.exp * 1000 <= Date.now() + EXPIRY_LEEWAY_MS;
  } catch {
    return true;
  }
}

async function tryRefresh(refreshToken: string): Promise<RefreshedTokens | null> {
  const gateway = process.env.API_GATEWAY_URL;
  if (!gateway) return null;
  try {
    const response = await fetch(`${gateway}/api/v1/auth/refresh`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
      cache: 'no-store',
    });
    if (!response.ok) return null;
    return (await response.json()) as RefreshedTokens;
  } catch {
    return null;
  }
}

/** Only run on protected pages; BFF routes and static assets refresh/serve themselves. */
export const config = {
  matcher: ['/dashboard/:path*'],
};
