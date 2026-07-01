import type { Metadata } from 'next';
import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { SignupForm } from '@/features/auth/components/signup-form';
import { BrandTestimonialPanel } from '@/features/auth/components/brand-testimonial-panel';
import { Logo } from '@/shared/ui/logo';
import { ThemeToggle } from '@/features/theming/components/theme-toggle';
import { LocaleToggle } from '@/features/theming/components/locale-toggle';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('signup');
  return { title: t('title') };
}

export default async function SignupPage() {
  const [tSignup, tCommon] = await Promise.all([
    getTranslations('signup'),
    getTranslations('common'),
  ]);

  return (
    <div className="grid min-h-dvh lg:grid-cols-2">
      {/* Branded panel — desktop only (S24). */}
      <div className="hidden lg:block">
        <BrandTestimonialPanel />
      </div>

      {/* Form side — the only thing visible on mobile. */}
      <div className="flex flex-col">
        <header className="flex items-center justify-between p-6">
          <Link href="/" aria-label={tCommon('appName')} className="lg:invisible">
            <Logo label={tCommon('appName')} />
          </Link>
          <div className="flex items-center gap-1">
            <LocaleToggle />
            <ThemeToggle />
          </div>
        </header>

        <main className="flex flex-1 items-center justify-center px-6 pb-16">
          <div className="w-full max-w-sm space-y-8">
            <div className="space-y-2 text-center lg:text-left">
              <h1 className="text-3xl font-bold tracking-tight">{tSignup('title')}</h1>
              <p className="text-muted-foreground">{tSignup('subtitle')}</p>
            </div>

            <SignupForm />

            <p className="text-center text-sm text-muted-foreground lg:text-left">
              {tSignup('haveAccount')}{' '}
              <Link
                href="/login"
                className="font-medium text-primary underline-offset-4 hover:underline"
              >
                {tSignup('signIn')}
              </Link>
            </p>
          </div>
        </main>
      </div>
    </div>
  );
}
