import { notFound } from 'next/navigation';
import { getTranslations } from 'next-intl/server';
import { getServerUser } from '@/features/auth/api/get-server-user';
import { ChatWidgetSettingsForm } from '@/features/settings/components/chat-widget-form';

export default async function ChatWidgetSettingsPage() {
  const t = await getTranslations('settings.channels.widget');

  // Gate at the route boundary, not just in the client form (audit M3 / S9 deny-by-default).
  // The server still authorizes every API call — this prevents even routing here without permission.
  const user = await getServerUser();
  const canManage = user?.permissions.includes('*') || user?.permissions.includes('settings:manage');
  if (!canManage) {
    notFound();
  }

  return (
    <section aria-label={t('title')} className="space-y-4">
      <div>
        <h2 className="text-base font-semibold tracking-tight">{t('title')}</h2>
      </div>
      <ChatWidgetSettingsForm />
    </section>
  );
}
