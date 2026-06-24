import 'server-only';

import { cookies } from 'next/headers';
import type { NextResponse } from 'next/server';
import type { AuthTokens } from '@/features/auth/model/auth.types';

/**
 * The ONLY place auth-token cookies are defined (frontend standard S3).
 *
 * Access + refresh tokens live exclusively in HttpOnly + SameSite=Strict cookies set
 * by the BFF route handlers — never in localStorage, a JS-readable cookie, or any
 * client store. `Secure` is enabled outside development so http://localhost dev still
 * works (browsers reject Secure cookies over plain http on non-localhost origins).
 */

const ACCESS_COOKIE = 'nx_at';
const REFRESH_COOKIE = 'nx_rt';
const CHALLENGE_COOKIE = 'nx_2fa';
const REFRESH_MAX_AGE_SECONDS = 60 * 60 * 24 * 14; // 14 days — mirrors backend refresh lifetime
const CHALLENGE_MAX_AGE_SECONDS = 5 * 60; // 5 min — mirrors the backend 2FA challenge token lifetime

const baseCookie = {
  httpOnly: true,
  secure: process.env.NODE_ENV !== 'development',
  sameSite: 'strict' as const,
  path: '/',
};

export interface SessionTokens {
  accessToken: string;
  refreshToken: string;
}

/** Read the current session tokens from the request cookies (server only). */
export async function readSession(): Promise<SessionTokens | null> {
  const jar = await cookies();
  const accessToken = jar.get(ACCESS_COOKIE)?.value;
  const refreshToken = jar.get(REFRESH_COOKIE)?.value;
  return accessToken && refreshToken ? { accessToken, refreshToken } : null;
}

/** Persist freshly issued/rotated tokens onto a response (login or silent refresh). */
export function writeSession(res: NextResponse, tokens: AuthTokens): void {
  res.cookies.set(ACCESS_COOKIE, tokens.accessToken, {
    ...baseCookie,
    maxAge: tokens.expiresInSeconds,
  });
  res.cookies.set(REFRESH_COOKIE, tokens.refreshToken, {
    ...baseCookie,
    maxAge: REFRESH_MAX_AGE_SECONDS,
  });
}

/** Clear the session (logout, or a dead refresh token). */
export function clearSession(res: NextResponse): void {
  res.cookies.set(ACCESS_COOKIE, '', { ...baseCookie, maxAge: 0 });
  res.cookies.set(REFRESH_COOKIE, '', { ...baseCookie, maxAge: 0 });
}

/**
 * Stash the short-lived 2FA challenge token (step 1 of login) in an HttpOnly cookie so it
 * never reaches JS (S3). The verify route reads it back to complete step 2. It is NOT a
 * session token — it only proves the password step passed — but keeping it out of the
 * client follows the same boundary as the access/refresh tokens.
 */
export function writeChallenge(res: NextResponse, challengeToken: string): void {
  res.cookies.set(CHALLENGE_COOKIE, challengeToken, {
    ...baseCookie,
    maxAge: CHALLENGE_MAX_AGE_SECONDS,
  });
}

/** Clear the 2FA challenge cookie (after a successful verify, or to reset). */
export function clearChallenge(res: NextResponse): void {
  res.cookies.set(CHALLENGE_COOKIE, '', { ...baseCookie, maxAge: 0 });
}

export { REFRESH_COOKIE, CHALLENGE_COOKIE };
