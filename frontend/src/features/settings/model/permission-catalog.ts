import { Shield, SlidersHorizontal, Target, Users, type LucideIcon } from 'lucide-react';
import type { PermissionCategory, PermissionItem } from './roles.types';

/**
 * Frontend presentation + preset logic for the role editor. The backend owns WHICH
 * permissions exist (GET /roles/permissions); this file only decides display order, icons,
 * and the None/Read-only/Standard/Full preset ladder. Pure functions here are unit-tested.
 */

/** Module display order (unknown modules fall to the end, alphabetically). */
const MODULE_ORDER = ['leads', 'users', 'roles', 'workspace'];

/** One lucide icon per module (single icon set — S28). */
export const MODULE_ICONS: Record<string, LucideIcon> = {
  leads: Target,
  users: Users,
  roles: Shield,
  workspace: SlidersHorizontal,
};

/** Category order within a module card, matching the screenshot (ACCESS → MANAGE → OPERATIONS). */
export const CATEGORY_ORDER: readonly PermissionCategory[] = ['Access', 'Manage', 'Operations'];

export type Preset = 'none' | 'read-only' | 'standard' | 'full';
export const PRESETS: readonly Preset[] = ['none', 'read-only', 'standard', 'full'];

/** Which categories each preset grants (the escalation ladder — mirrors the backend comment). */
const PRESET_CATEGORIES: Record<Preset, readonly PermissionCategory[]> = {
  none: [],
  'read-only': ['Access'],
  standard: ['Access', 'Manage'],
  full: ['Access', 'Manage', 'Operations'],
};

export interface CategoryGroup {
  category: PermissionCategory;
  items: PermissionItem[];
}
export interface ModuleGroup {
  module: string;
  total: number;
  categories: CategoryGroup[];
}

/** Group the flat catalog into ordered modules, each with ordered category buckets. */
export function groupCatalog(catalog: PermissionItem[]): ModuleGroup[] {
  const byModule = new Map<string, PermissionItem[]>();
  for (const item of catalog) {
    const list = byModule.get(item.module) ?? [];
    list.push(item);
    byModule.set(item.module, list);
  }

  const orderOf = (m: string) => {
    const i = MODULE_ORDER.indexOf(m);
    return i === -1 ? MODULE_ORDER.length : i;
  };

  return [...byModule.keys()]
    .sort((a, b) => orderOf(a) - orderOf(b) || a.localeCompare(b))
    .map((module) => {
      const items = byModule.get(module) ?? [];
      const categories = CATEGORY_ORDER.map((category) => ({
        category,
        items: items.filter((i) => i.category === category),
      })).filter((g) => g.items.length > 0);
      return { module, total: items.length, categories };
    });
}

/** The keys a preset grants within a given set of permission items. */
export function presetKeys(items: PermissionItem[], preset: Preset): string[] {
  const cats = PRESET_CATEGORIES[preset];
  return items.filter((i) => cats.includes(i.category)).map((i) => i.key);
}

/** Apply a preset to the whole catalog → the full granted set. */
export function applyGlobalPreset(catalog: PermissionItem[], preset: Preset): Set<string> {
  return new Set(presetKeys(catalog, preset));
}

/** Apply a preset to one module, preserving every other module's current grants. */
export function applyModulePreset(
  current: Set<string>,
  moduleItems: PermissionItem[],
  preset: Preset,
): Set<string> {
  const next = new Set(current);
  for (const item of moduleItems) next.delete(item.key);
  for (const key of presetKeys(moduleItems, preset)) next.add(key);
  return next;
}

/** How many of `items` are granted in the working set. */
export function countGranted(items: PermissionItem[], granted: ReadonlySet<string>): number {
  return items.reduce((n, i) => n + (granted.has(i.key) ? 1 : 0), 0);
}

/** i18n leaf-key for a permission (':' isn't a valid message-key separator → '_'). */
export function permissionLabelKey(key: string): string {
  return key.replace(/:/g, '_');
}
