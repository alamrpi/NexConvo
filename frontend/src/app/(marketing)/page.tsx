import { getTranslations } from 'next-intl/server';
import { HeroSection } from '@/features/marketing/components/hero-section';

export default async function LandingPage() {
  const t = await getTranslations('landing');

  return (
    <>
      <HeroSection />

      {/* Social-proof band — restrained, token-based (S28). */}
      <section className="border-t border-border bg-muted/30 py-10" aria-label={t('trustedBy')}>
        <div className="container flex flex-col items-center gap-6">
          <p className="text-sm font-medium text-muted-foreground">{t('trustedBy')}</p>
          <div className="flex flex-wrap items-center justify-center gap-x-10 gap-y-4 opacity-70">
            {['Acme', 'Northwind', 'Globex', 'Initech', 'Umbrella'].map((name) => (
              <span key={name} className="text-lg font-semibold tracking-tight text-foreground/70">
                {name}
              </span>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
