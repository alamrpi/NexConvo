'use client';

import * as React from 'react';
import { useSessionStore } from '../model/session.store';
import type { CurrentUser } from '../model/auth.types';

/**
 * Hydrates the Zustand session store from the server-fetched user (S7). The shell paints with
 * the user (no auth flash) via a first-render hydration, and **re-syncs whenever the server
 * provides a fresh user** — the (dashboard) layout is persistent and doesn't remount, so after a
 * `router.refresh()` (email verified, permissions or the workspace-2FA flag changed) the store
 * must be updated or banners/RBAC gates would show stale state.
 */
export function SessionProvider({
  initialUser,
  children,
}: {
  initialUser: CurrentUser;
  children: React.ReactNode;
}) {
  const hydrated = React.useRef(false);
  if (!hydrated.current) {
    useSessionStore.setState({ user: initialUser });
    hydrated.current = true;
  }
  React.useEffect(() => {
    useSessionStore.setState({ user: initialUser });
  }, [initialUser]);
  return <>{children}</>;
}
