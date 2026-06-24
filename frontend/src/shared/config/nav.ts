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
 * `labelKey`/`section` resolve through the i18n layer (S12) — never a hardcoded string.
 */
export type NavSection = 'workspace' | 'engage' | 'insights' | 'account';

export interface NavItem {
  /** i18n key under `nav.dashboard`. */
  labelKey: string;
  href: string;
  icon: LucideIcon;
  /** Grouping for the sidebar; header label resolves under `nav.dashboard.sections`. */
  section: NavSection;
}

/** Render order for the grouped sidebar sections. */
export const navSections: readonly NavSection[] = ['workspace', 'engage', 'insights', 'account'];

export const dashboardNav: readonly NavItem[] = [
  { labelKey: 'overview', href: '/dashboard', icon: LayoutDashboard, section: 'workspace' },
  { labelKey: 'inbox', href: '/dashboard/inbox', icon: Inbox, section: 'workspace' },
  { labelKey: 'leads', href: '/dashboard/leads', icon: Users, section: 'workspace' },
  { labelKey: 'projects', href: '/dashboard/projects', icon: FolderKanban, section: 'workspace' },
  { labelKey: 'campaigns', href: '/dashboard/campaigns', icon: Megaphone, section: 'engage' },
  { labelKey: 'automation', href: '/dashboard/automation', icon: Workflow, section: 'engage' },
  { labelKey: 'analytics', href: '/dashboard/analytics', icon: BarChart3, section: 'insights' },
  { labelKey: 'settings', href: '/dashboard/settings', icon: Settings, section: 'account' },
] as const;
