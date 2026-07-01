import {
  LayoutDashboard,
  Users,
  FolderKanban,
  Megaphone,
  Workflow,
  BarChart3,
  Settings,
  MessageSquare,
  BarChart2,
  FlaskConical,
  FileBarChart,
  Zap,
  BookOpen,
  Bot,
  Wrench,
  type LucideIcon,
} from 'lucide-react';

export type NavSection = 'workspace' | 'engage' | 'chat' | 'insights' | 'account';

export interface NavItem {
  labelKey: string;
  href: string;
  icon: LucideIcon;
  section: NavSection;
  children?: readonly NavItem[];
}

export const navSections: readonly NavSection[] = ['workspace', 'engage', 'chat', 'insights', 'account'];

export const dashboardNav: readonly NavItem[] = [
  { labelKey: 'overview',   href: '/dashboard',          icon: LayoutDashboard, section: 'workspace' },
  { labelKey: 'leads',      href: '/dashboard/leads',    icon: Users,           section: 'workspace' },
  { labelKey: 'projects',   href: '/dashboard/projects', icon: FolderKanban,    section: 'workspace' },
  { labelKey: 'campaigns',  href: '/dashboard/campaigns',icon: Megaphone,       section: 'engage' },
  { labelKey: 'automation', href: '/dashboard/automation',icon: Workflow,       section: 'engage' },

  // ── Chat ────────────────────────────────────────────────────────────────────
  { labelKey: 'chatAnalytics',  href: '/dashboard/chat/analytics',  icon: BarChart2,    section: 'chat' },
  { labelKey: 'chatInbox',      href: '/dashboard/chat/inbox',      icon: MessageSquare, section: 'chat' },
  { labelKey: 'chatPlayground', href: '/dashboard/chat/playground', icon: FlaskConical, section: 'chat' },
  {
    labelKey: 'chatReports',
    href: '/dashboard/chat/reports',
    icon: FileBarChart,
    section: 'chat',
    // placeholder children — expand when Reports sub-pages are ready
    children: [],
  },
  {
    labelKey: 'chatSettings',
    href: '/dashboard/chat/settings',
    icon: Settings,
    section: 'chat',
    children: [
      { labelKey: 'chatChannels',  href: '/dashboard/chat/settings/channels',  icon: Zap,      section: 'chat' },
      { labelKey: 'chatKnowledge', href: '/dashboard/chat/settings/knowledge', icon: BookOpen, section: 'chat' },
      { labelKey: 'chatAi',        href: '/dashboard/chat/settings/ai',        icon: Bot,      section: 'chat' },
      { labelKey: 'chatTools',     href: '/dashboard/chat/settings/tools',     icon: Wrench,   section: 'chat' },
    ],
  },

  // ── Insights / Account ───────────────────────────────────────────────────────
  { labelKey: 'analytics', href: '/dashboard/analytics', icon: BarChart3, section: 'insights' },
  { labelKey: 'settings',  href: '/dashboard/settings',  icon: Settings,  section: 'account' },
] as const;
