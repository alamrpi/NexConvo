import { redirect } from 'next/navigation';
import { getServerUser } from '@/features/auth/api/get-server-user';
import { SessionProvider } from '@/features/auth/components/session-provider';
import { EmailVerificationBanner } from '@/features/account/components/email-verification-banner';
import { TwoFactorRequiredBanner } from '@/features/account/components/two-factor-required-banner';
import { AppSidebar } from '@/features/dashboard/components/app-sidebar';
import { AppTopbar } from '@/features/dashboard/components/app-topbar';

/**
 * Protected dashboard shell (frontend standards S7, S9). Resolves the session
 * server-side and redirects unauthenticated users to /login (defense-in-depth on top
 * of the middleware guard), then hydrates the session store with no auth flash.
 *
 * Desktop: fixed w-64 sidebar + sticky topbar. Mobile: sticky topbar with hamburger →
 * slide-out sheet. The main area uses a muted background so cards pop in light mode (S25).
 */
export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  const user = await getServerUser();
  if (!user) {
    redirect('/login');
  }

  return (
    <SessionProvider initialUser={user}>
      <div className="flex min-h-dvh bg-background">
        <AppSidebar />
        <div className="flex min-w-0 flex-1 flex-col">
          <AppTopbar />
          <EmailVerificationBanner />
          <TwoFactorRequiredBanner />
          <main className="flex-1 bg-muted/40 p-4 dark:bg-background sm:p-6">{children}</main>
        </div>
      </div>
    </SessionProvider>
  );
}
