import { getTranslations } from 'next-intl/server';
import { MembersSection } from '@/features/settings/components/members-section';

export default async function MembersSettingsPage() {
  const t = await getTranslations('settings.members');
  return (
    <section aria-label={t('title')} className="space-y-1">
      <h2 className="text-xl font-semibold tracking-tight">{t('title')}</h2>
      <p className="max-w-prose text-sm text-muted-foreground">{t('description')}</p>
      <div className="pt-4">
        <MembersSection />
      </div>
    </section>
  );
}
