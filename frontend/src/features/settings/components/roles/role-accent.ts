import type { Role } from '../../model/roles.types';

/**
 * Categorical avatar accent for a role (like data-viz colors — distinct, legible in both
 * themes with white text). Owner is special-cased to amber + crown; others get a stable
 * color from a small palette keyed by role id.
 */
const PALETTE = ['bg-violet-600', 'bg-emerald-600', 'bg-sky-600', 'bg-rose-600', 'bg-cyan-600', 'bg-fuchsia-600'];

export interface RoleAccent {
  className: string;
  initials: string;
  isOwner: boolean;
}

export function roleAccent(role: Role): RoleAccent {
  const isOwner = role.grantsAll;
  const initials =
    role.name
      .split(/\s+/)
      .map((p) => p.charAt(0))
      .filter(Boolean)
      .slice(0, 2)
      .join('')
      .toUpperCase() || 'R';

  if (isOwner) return { className: 'bg-amber-500', initials, isOwner: true };

  let hash = 0;
  for (const ch of role.id) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  return { className: PALETTE[hash % PALETTE.length] ?? 'bg-violet-600', initials, isOwner: false };
}
