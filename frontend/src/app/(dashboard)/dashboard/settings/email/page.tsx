import { getTranslations } from 'next-intl/server';
import { EmailSettingsForm } from '@/features/settings/components/email-settings-form';

export default async function EmailSettingsPage() {
  const t = await getTranslations('settings.email');

  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
        <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <EmailSettingsForm />
    </section>
  );
}
