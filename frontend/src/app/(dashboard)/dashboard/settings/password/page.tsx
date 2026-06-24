import { getTranslations } from 'next-intl/server';
import { PasswordForm } from '@/features/account/components/password-form';

export default async function PasswordSettingsPage() {
  const t = await getTranslations('settings.password');
  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
        <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <PasswordForm />
    </section>
  );
}
