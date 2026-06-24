import { getTranslations } from 'next-intl/server';
import { ProfileForm } from '@/features/account/components/profile-form';

export default async function AccountSettingsPage() {
  const t = await getTranslations('settings.account');
  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
        <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <ProfileForm />
    </section>
  );
}
