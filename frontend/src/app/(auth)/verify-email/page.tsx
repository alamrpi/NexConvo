import type { Metadata } from 'next';
import { getTranslations } from 'next-intl/server';
import { VerifyEmailStatus } from '@/features/auth/components/verify-email-status';

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations('verifyEmail');
  return { title: t('title') };
}

export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;
  return (
    <main className="flex min-h-dvh items-center justify-center px-6 py-16">
      <div className="w-full max-w-sm">
        <VerifyEmailStatus token={token ?? ''} />
      </div>
    </main>
  );
}
