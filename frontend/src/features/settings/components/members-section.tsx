'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2, Mail, UserPlus } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Skeleton } from '@/shared/ui/skeleton';
import { useSessionStore } from '@/features/auth/model/session.store';
import { inviteMemberSchema, type InviteMemberValues } from '../model/members';
import { usePendingInvitations, useInviteMember } from '../api/use-members';
import { useRoles } from '../api/use-roles';
import { UsersSection } from './users-section';

export function MembersSection() {
  const t = useTranslations('settings.members');
  const canRead = useSessionStore((s) => s.hasPermission('users:read'));
  const canInvite = useSessionStore((s) => s.hasPermission('users:invite'));

  if (!canRead) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  return (
    <div className="space-y-6">
      <UsersSection />
      {canInvite && (
        <div className="space-y-6">
          <InviteForm />
          <PendingList />
        </div>
      )}
    </div>
  );
}

function InviteForm() {
  const t = useTranslations('settings.members');
  const invite = useInviteMember();
  // Real roles (Owner is not invitable) populate the dropdown — no hardcoded role names.
  const { data: roles } = useRoles();
  const invitableRoles = (roles ?? []).filter((r) => !r.grantsAll);
  const defaultRole = invitableRoles.some((r) => r.name === 'Member') ? 'Member' : invitableRoles[0]?.name ?? '';

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<InviteMemberValues>({
    resolver: zodResolver(inviteMemberSchema),
    mode: 'onTouched',
    values: { email: '', roleName: defaultRole },
  });

  const onSubmit = handleSubmit(async (values) => {
    const ok = await invite.mutateAsync(values).then(() => true).catch(() => false);
    if (ok) {
      reset({ email: '', roleName: defaultRole });
    }
  });
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || invite.isPending;

  return (
    <Card>
      <CardContent className="p-5">
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          {invite.isSuccess && <p role="status" className="text-sm text-muted-foreground">{t('invited')}</p>}
          {invite.isError && <p role="alert" className="text-sm text-destructive">{t('inviteFailed')}</p>}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-[1fr_160px]">
            <div className="space-y-2">
              <Label htmlFor="invite-email">{t('emailLabel')}</Label>
              <Input id="invite-email" type="email" aria-invalid={!!errors.email} {...register('email')} />
              {errors.email && <p role="alert" className="text-sm text-destructive">{errorText(errors.email.message)}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="invite-role">{t('roleLabel')}</Label>
              <select
                id="invite-role"
                className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm transition-colors hover:border-ring/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
                {...register('roleName')}
              >
                {invitableRoles.map((role) => (
                  <option key={role.id} value={role.name}>
                    {role.name}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <Button type="submit" size="sm" disabled={busy}>
            {busy ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                {t('inviting')}
              </>
            ) : (
              <>
                <UserPlus aria-hidden="true" />
                {t('invite')}
              </>
            )}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

function PendingList() {
  const t = useTranslations('settings.members');
  const { data, isLoading, isError } = usePendingInvitations();

  if (isLoading) {
    return <Skeleton className="h-24 w-full" />;
  }
  if (isError) {
    return <p role="alert" className="text-sm text-destructive">{t('loadError')}</p>;
  }
  if (!data || data.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-md border border-dashed border-border py-10 text-center text-sm text-muted-foreground">
        <Mail className="h-6 w-6" aria-hidden="true" />
        {t('empty')}
      </div>
    );
  }

  return (
    <section aria-label={t('pendingTitle')} className="space-y-2">
      <h3 className="text-sm font-medium">{t('pendingTitle')}</h3>
      <ul className="divide-y divide-border rounded-md border border-border">
        {data.map((invitation) => (
          <li key={invitation.id} className="flex items-center justify-between gap-3 px-4 py-3 text-sm">
            <span className="min-w-0 truncate">{invitation.email}</span>
            <Badge variant="secondary">{invitation.roleName}</Badge>
          </li>
        ))}
      </ul>
    </section>
  );
}
