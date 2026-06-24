import { describe, expect, it } from 'vitest';
import {
  applyGlobalPreset,
  applyModulePreset,
  countGranted,
  groupCatalog,
  presetKeys,
} from './permission-catalog';
import type { PermissionItem } from './roles.types';

// The real NexConvo catalog (8 perms / 4 modules).
const catalog: PermissionItem[] = [
  { key: 'users:read', module: 'users', category: 'Access' },
  { key: 'users:manage', module: 'users', category: 'Manage' },
  { key: 'users:invite', module: 'users', category: 'Operations' },
  { key: 'roles:manage', module: 'roles', category: 'Manage' },
  { key: 'settings:manage', module: 'workspace', category: 'Manage' },
  { key: 'tenant:manage', module: 'workspace', category: 'Operations' },
  { key: 'leads:read', module: 'leads', category: 'Access' },
  { key: 'leads:write', module: 'leads', category: 'Manage' },
];
const usersItems = catalog.filter((c) => c.module === 'users');

describe('groupCatalog', () => {
  it('orders modules leads → users → roles → workspace and buckets by category', () => {
    const groups = groupCatalog(catalog);
    expect(groups.map((g) => g.module)).toEqual(['leads', 'users', 'roles', 'workspace']);
    const users = groups.find((g) => g.module === 'users');
    expect(users?.total).toBe(3);
    expect(users?.categories.map((c) => c.category)).toEqual(['Access', 'Manage', 'Operations']);
  });

  it('omits empty category buckets (roles has only Manage)', () => {
    const roles = groupCatalog(catalog).find((g) => g.module === 'roles');
    expect(roles?.categories.map((c) => c.category)).toEqual(['Manage']);
  });
});

describe('presetKeys (escalation ladder)', () => {
  it('none → nothing, read-only → Access, standard → Access+Manage, full → all', () => {
    expect(presetKeys(usersItems, 'none')).toEqual([]);
    expect(presetKeys(usersItems, 'read-only')).toEqual(['users:read']);
    expect(presetKeys(usersItems, 'standard').sort()).toEqual(['users:manage', 'users:read']);
    expect(presetKeys(usersItems, 'full').sort()).toEqual(['users:invite', 'users:manage', 'users:read']);
  });
});

describe('applyGlobalPreset', () => {
  it('read-only across the catalog = every Access key', () => {
    expect([...applyGlobalPreset(catalog, 'read-only')].sort()).toEqual(['leads:read', 'users:read']);
  });
  it('full = every key; none = empty', () => {
    expect(applyGlobalPreset(catalog, 'full').size).toBe(8);
    expect(applyGlobalPreset(catalog, 'none').size).toBe(0);
  });
});

describe('applyModulePreset', () => {
  it('replaces only the target module, preserving other modules', () => {
    const start = new Set(catalog.map((c) => c.key)); // all granted
    const next = applyModulePreset(start, usersItems, 'none');
    expect(next.has('users:read')).toBe(false);
    expect(next.has('users:invite')).toBe(false);
    expect(next.has('leads:read')).toBe(true); // untouched
    expect(next.has('roles:manage')).toBe(true); // untouched
  });
});

describe('countGranted', () => {
  it('counts items present in the granted set', () => {
    expect(countGranted(usersItems, new Set(['users:read', 'leads:read']))).toBe(1);
  });
});
