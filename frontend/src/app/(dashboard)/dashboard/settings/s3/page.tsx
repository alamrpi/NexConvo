import { getTranslations } from 'next-intl/server';
import { S3ConfigForm } from '@/features/settings/components/s3-config-form';

export async function generateMetadata() {
  const t = await getTranslations('settings.s3');
  return { title: t('title') };
}

export default async function S3SettingsPage() {
  const t = await getTranslations('settings.s3');

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-lg font-medium text-foreground">{t('title')}</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">{t('description')}</p>
      </div>
      <S3ConfigForm />
    </div>
  );
}
