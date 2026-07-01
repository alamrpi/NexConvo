import { getTranslations } from 'next-intl/server';
import { AiSettingsForm } from '@/features/settings/components/ai-settings-form';

export async function generateMetadata() {
  const t = await getTranslations('settings.ai');
  return { title: t('title') };
}

export default async function AiSettingsPage() {
  const t = await getTranslations('settings.ai');
  
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-lg font-medium">{t('title')}</h1>
        <p className="text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <AiSettingsForm />
    </div>
  );
}
