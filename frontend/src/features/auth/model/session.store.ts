import { create } from 'zustand';
import type { CurrentUser } from './auth.types';

/**
 * Client session state (frontend standard S7). Holds the authenticated user and the
 * RBAC helpers the UI gates on. This is NOT a cache of server data — it is hydrated
 * once from the server (see SessionProvider) and updated on login/logout. Tokens
 * never live here (S3).
 */
interface SessionState {
  user: CurrentUser | null;
  setUser: (user: CurrentUser | null) => void;
  /** True if the user holds the permission, honoring the `*` wildcard (S9). */
  hasPermission: (key: string) => boolean;
  hasRole: (role: string) => boolean;
}

export const useSessionStore = create<SessionState>((set, get) => ({
  user: null,
  setUser: (user) => set({ user }),
  hasPermission: (key) => {
    const permissions = get().user?.permissions ?? [];
    return permissions.includes('*') || permissions.includes(key);
  },
  hasRole: (role) => get().user?.roles.includes(role) ?? false,
}));
