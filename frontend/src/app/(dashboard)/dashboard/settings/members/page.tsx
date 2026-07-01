import { getTranslations } from 'next-intl/server';
import { MembersSection } from '@/features/settings/components/members-section';

export default async function MembersSettingsPage() {
  const t = await getTranslations('settings.members');
  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
        <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <MembersSection />
    </section>
  );
}
