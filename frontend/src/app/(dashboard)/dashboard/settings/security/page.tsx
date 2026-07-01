import { getTranslations } from 'next-intl/server';
import { TwoFactorSection } from '@/features/account/components/two-factor-section';
import { WorkspaceTwoFactorPolicy } from '@/features/settings/components/workspace-two-factor-policy';

export default async function SecuritySettingsPage() {
  const t = await getTranslations('settings.security');
  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
        <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <TwoFactorSection />
      <WorkspaceTwoFactorPolicy />
    </section>
  );
}
