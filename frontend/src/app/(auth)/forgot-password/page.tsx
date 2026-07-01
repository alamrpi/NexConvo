import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { ForgotPasswordForm } from '@/features/auth/components/forgot-password-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('forgotPassword');
  return { title: t('title') };
}

export default async function ForgotPasswordPage() {
  const t = await getTranslations('forgotPassword');
  return (
    <main className="flex min-h-dvh items-center justify-center px-6 py-16">
      <div className="w-full max-w-sm space-y-8">
        <div className="space-y-2 text-center">
          <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
          <p className="text-muted-foreground">{t('subtitle')}</p>
        </div>
        <ForgotPasswordForm />
      </div>
    </main>
  );
}
