import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { Button } from '@/shared/ui/button';
import { Logo } from '@/shared/ui/logo';
import { ThemeToggle } from '@/features/theming/components/theme-toggle';
import { LocaleToggle } from '@/features/theming/components/locale-toggle';

/**
 * Marketing top nav. Server component; only the toggles are client leaves (S6).
 * Mobile-first: primary nav links collapse, the Login CTA stays reachable (S24).
 */
export async function MarketingHeader() {
  const [tNav, tCommon] = await Promise.all([
    getTranslations('nav.marketing'),
    getTranslations('common'),
  ]);

  const links = [
    { key: 'features', href: '#features' },
    { key: 'solutions', href: '#solutions' },
    { key: 'pricing', href: '#pricing' },
    { key: 'docs', href: '#docs' },
  ] as const;

  return (
    <header className="sticky top-0 z-40 w-full border-b border-border bg-background/80 backdrop-blur">
      <div className="container flex h-16 items-center justify-between gap-4">
        <Link href="/" aria-label={tCommon('appName')}>
          <Logo label={tCommon('appName')} />
        </Link>

        <nav className="hidden items-center gap-1 md:flex" aria-label="Primary">
          {links.map((link) => (
            <Button key={link.key} asChild variant="ghost" size="sm">
              <Link href={link.href}>{tNav(link.key)}</Link>
            </Button>
          ))}
        </nav>

        <div className="flex items-center gap-1">
          <LocaleToggle />
          <ThemeToggle />
          <Button asChild size="sm" className="ml-1">
            <Link href="/login">{tNav('login')}</Link>
          </Button>
        </div>
      </div>
    </header>
  );
}
