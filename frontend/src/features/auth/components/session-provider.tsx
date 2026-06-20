'use client';

import * as React from 'react';
import { useSessionStore } from '../model/session.store';
import type { CurrentUser } from '../model/auth.types';

/**
 * Hydrates the Zustand session store from the server-fetched user exactly once, so the
 * authenticated shell renders with no auth flash and no SSR/client mismatch (S7).
 * Mounted by the protected (dashboard) layout, which has already guarded access.
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
  return <>{children}</>;
}
