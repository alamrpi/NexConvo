import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { MarketingHeader } from '@/features/marketing/components/marketing-header';
import { Logo } from '@/shared/ui/logo';

export default async function MarketingLayout({ children }: { children: React.ReactNode }) {
  const [tCommon, tLanding] = await Promise.all([
    getTranslations('common'),
    getTranslations('landing'),
  ]);
  const year = new Date().getFullYear();

  return (
    <div className="flex min-h-dvh flex-col">
      <MarketingHeader />
      <main className="flex-1">{children}</main>
      <footer className="border-t border-border">
        <div className="container flex flex-col items-center justify-between gap-4 py-8 sm:flex-row">
          <Logo label={tCommon('appName')} className="text-base" />
          <p className="text-sm text-muted-foreground">
            © {year} {tCommon('appName')}. {tLanding('footer.rights')}
          </p>
          <nav className="flex items-center gap-6 text-sm text-muted-foreground" aria-label="Footer">
            <Link href="#privacy" className="transition-colors hover:text-foreground">
              {tLanding('footer.privacy')}
            </Link>
            <Link href="#terms" className="transition-colors hover:text-foreground">
              {tLanding('footer.terms')}
            </Link>
          </nav>
        </div>
      </footer>
    </div>
  );
}
