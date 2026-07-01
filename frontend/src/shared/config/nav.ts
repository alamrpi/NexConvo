import {
  LayoutDashboard,
  Inbox,
  Users,
  FolderKanban,
  Megaphone,
  Workflow,
  BarChart3,
  Settings,
  type LucideIcon,
} from 'lucide-react';

/**
 * Dashboard navigation defined ONCE as data (frontend standard S5 / DRY).
 * Consumed by both the desktop sidebar and the mobile sheet.
 * `labelKey` resolves through the i18n layer (S12) — never a hardcoded string.
 */
export interface NavItem {
  /** i18n key under `nav.dashboard`. */
  labelKey: string;
  href: string;
  icon: LucideIcon;
}

export const dashboardNav: readonly NavItem[] = [
  { labelKey: 'overview', href: '/dashboard', icon: LayoutDashboard },
  { labelKey: 'inbox', href: '/dashboard/inbox', icon: Inbox },
  { labelKey: 'leads', href: '/dashboard/leads', icon: Users },
  { labelKey: 'projects', href: '/dashboard/projects', icon: FolderKanban },
  { labelKey: 'campaigns', href: '/dashboard/campaigns', icon: Megaphone },
  { labelKey: 'automation', href: '/dashboard/automation', icon: Workflow },
  { labelKey: 'analytics', href: '/dashboard/analytics', icon: BarChart3 },
  { labelKey: 'settings', href: '/dashboard/settings', icon: Settings },
] as const;
