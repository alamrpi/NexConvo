import { getTranslations } from 'next-intl/server';
import { ChatWidgetSettingsForm } from '@/features/settings/components/chat-widget-form';

export default async function ChatWidgetSettingsPage() {
  const t = await getTranslations('settings.channels.widget');

  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
      </div>
      <ChatWidgetSettingsForm />
    </section>
  );
}
