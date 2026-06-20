import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { ArrowRight, CalendarClock, Sparkles, ShieldCheck } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { MotionIn } from './motion-in';

/**
 * Landing hero. Mobile-first centered column that widens at md/lg (S24); restrained
 * indigo accent (S28); subtle staggered entrance via the MotionIn client leaf (S6, S26).
 */
export async function HeroSection() {
  const t = await getTranslations('landing');

  return (
    <section className="relative overflow-hidden">
      {/* Decorative background — subtle, token-based, non-interactive. */}
      <div aria-hidden className="pointer-events-none absolute inset-0 -z-10">
        <div className="absolute left-1/2 top-[-10%] h-[480px] w-[480px] -translate-x-1/2 rounded-full bg-primary/10 blur-3xl" />
        <div className="absolute inset-0 bg-[radial-gradient(hsl(var(--border))_1px,transparent_1px)] [background-size:24px_24px] opacity-40 [mask-image:radial-gradient(ellipse_at_center,black,transparent_75%)]" />
      </div>

      <div className="container flex flex-col items-center gap-8 py-20 text-center md:py-28 lg:py-32">
        <MotionIn>
          <span className="inline-flex items-center gap-2 rounded-full border border-border bg-background/60 px-3 py-1 text-sm text-muted-foreground shadow-sm">
            <Sparkles className="h-3.5 w-3.5 text-primary" />
            {t('badge')}
          </span>
        </MotionIn>

        <MotionIn delay={0.05}>
          <h1 className="mx-auto max-w-4xl text-balance text-4xl font-bold tracking-tight sm:text-5xl md:text-6xl">
            {t('headlinePart1')}{' '}
            <span className="bg-gradient-to-r from-primary to-indigo-400 bg-clip-text text-transparent">
              {t('headlineHighlight')}
            </span>
          </h1>
        </MotionIn>

        <MotionIn delay={0.1}>
          <p className="mx-auto max-w-2xl text-pretty text-lg leading-relaxed text-muted-foreground">
            {t('subheadline')}
          </p>
        </MotionIn>

        <MotionIn delay={0.15}>
          <div className="flex w-full flex-col items-center justify-center gap-3 sm:flex-row">
            <Button asChild size="lg" className="w-full sm:w-auto">
              <Link href="/login">
                {t('primaryCta')}
                <ArrowRight className="h-4 w-4" />
              </Link>
            </Button>
            <Button asChild size="lg" variant="outline" className="w-full sm:w-auto">
              <Link href="#demo">
                <CalendarClock className="h-4 w-4" />
                {t('secondaryCta')}
              </Link>
            </Button>
          </div>
        </MotionIn>

        <MotionIn delay={0.2}>
          <p className="flex items-center gap-2 text-sm text-muted-foreground">
            <ShieldCheck className="h-4 w-4 text-primary" />
            {t('noCreditCard')}
          </p>
        </MotionIn>
      </div>
    </section>
  );
}
