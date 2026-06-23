import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { AcceptInviteForm } from '@/features/auth/components/accept-invite-form';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('acceptInvite');
  return { title: t('title') };
}

export default async function AcceptInvitePage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;
  const t = await getTranslations('acceptInvite');
  return (
    <main className="flex min-h-dvh items-center justify-center px-6 py-16">
      <div className="w-full max-w-sm space-y-8">
        <div className="space-y-2 text-center">
          <h1 className="text-3xl font-bold tracking-tight">{t('title')}</h1>
          <p className="text-muted-foreground">{t('subtitle')}</p>
        </div>
        <AcceptInviteForm token={token ?? ''} />
      </div>
    </main>
  );
}
