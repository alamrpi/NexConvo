'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { CheckCircle2, Loader2, XCircle } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Card, CardContent } from '@/shared/ui/card';
import { Badge } from '@/shared/ui/badge';
import { Skeleton } from '@/shared/ui/skeleton';
import { useSessionStore } from '@/features/auth/model/session.store';
import { emailSettingsSchema, type EmailSettingsValues } from '../model/email-settings.schema';
import type { EmailSettings } from '../model/email-settings.types';
import { useEmailSettings } from '../api/use-email-settings';
import { useUpdateEmailSettings } from '../api/use-update-email-settings';
import { useSendTestEmail } from '../api/use-send-test-email';

/**
 * Email-settings section. Handles the query's loading/error states (S15/S27), gates on the
 * `settings:manage` permission (S9 — UX only; the server still authorizes), then renders the
 * provider form. Client validation is UX (S10); the secret is never displayed.
 */
export function EmailSettingsForm() {
  const t = useTranslations('settings.email');
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useEmailSettings();

  if (!canManage) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  if (isLoading) {
    return <FormSkeleton />;
  }

  if (isError || !data) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  return <EmailSettingsFields settings={data} />;
}

function FormSkeleton() {
  return (
    <Card>
      <CardContent className="space-y-6 pt-6">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="space-y-2">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-10 w-full" />
          </div>
        ))}
        <Skeleton className="h-10 w-40" />
      </CardContent>
    </Card>
  );
}

function EmailSettingsFields({ settings }: { settings: EmailSettings }) {
  const t = useTranslations('settings.email');
  const update = useUpdateEmailSettings();
  const sendTest = useSendTestEmail();
  const [replacingSecret, setReplacingSecret] = React.useState(!settings.hasSecret);

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<EmailSettingsValues>({
    resolver: zodResolver(emailSettingsSchema),
    mode: 'onTouched',
    defaultValues: {
      provider: settings.provider,
      fromName: settings.fromName,
      fromAddress: settings.fromAddress,
      isEnabled: settings.isEnabled,
      smtpHost: settings.smtpHost ?? '',
      smtpPort: settings.smtpPort || 587,
      smtpUsername: settings.smtpUsername ?? '',
      smtpUseSsl: settings.smtpUseSsl,
      secret: '',
    },
  });

  const provider = watch('provider');
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || update.isPending;

  const onSubmit = handleSubmit(async (values) => {
    await update.mutateAsync(values).catch(() => null);
  });

  return (
    <div className="space-y-4">
      <StatusBar settings={settings} />

      <Card>
        <CardContent className="pt-6">
          <form onSubmit={onSubmit} noValidate className="space-y-6">
            {update.isSuccess && (
              <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm">
                {t('saved')}
              </p>
            )}
            {update.isError && (
              <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {t('saveError')}
              </p>
            )}

            {/* Provider */}
            <fieldset className="space-y-2">
              <legend className="text-sm font-medium">{t('providerLabel')}</legend>
              <div className="flex gap-4">
                {(['Smtp', 'Resend'] as const).map((value) => (
                  <label key={value} className="flex cursor-pointer items-center gap-2 text-sm">
                    <input
                      type="radio"
                      value={value}
                      className="h-4 w-4 accent-primary"
                      {...register('provider')}
                    />
                    {t(`provider.${value}`)}
                  </label>
                ))}
              </div>
            </fieldset>

            {/* From identity */}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="fromName" label={t('fromNameLabel')} error={errorText(errors.fromName?.message)}>
                <Input id="fromName" {...register('fromName')} aria-invalid={!!errors.fromName} />
              </Field>
              <Field id="fromAddress" label={t('fromAddressLabel')} error={errorText(errors.fromAddress?.message)}>
                <Input id="fromAddress" type="email" {...register('fromAddress')} aria-invalid={!!errors.fromAddress} />
              </Field>
            </div>

            {/* SMTP-only transport fields */}
            {provider === 'Smtp' && (
              <div className="grid gap-4 sm:grid-cols-2">
                <Field id="smtpHost" label={t('smtpHostLabel')} error={errorText(errors.smtpHost?.message)}>
                  <Input id="smtpHost" {...register('smtpHost')} aria-invalid={!!errors.smtpHost} />
                </Field>
                <Field id="smtpPort" label={t('smtpPortLabel')} error={errorText(errors.smtpPort?.message)}>
                  <Input id="smtpPort" type="number" {...register('smtpPort', { valueAsNumber: true })} aria-invalid={!!errors.smtpPort} />
                </Field>
                <Field id="smtpUsername" label={t('smtpUsernameLabel')}>
                  <Input id="smtpUsername" autoComplete="off" {...register('smtpUsername')} />
                </Field>
                <label className="flex items-center gap-2 self-end pb-2 text-sm">
                  <input type="checkbox" className="h-4 w-4 accent-primary" {...register('smtpUseSsl')} />
                  {t('smtpUseSslLabel')}
                </label>
              </div>
            )}

            {/* Secret (SMTP password / Resend API key) */}
            <Field
              id="secret"
              label={provider === 'Resend' ? t('apiKeyLabel') : t('smtpPasswordLabel')}
              error={errorText(errors.secret?.message)}
            >
              {settings.hasSecret && !replacingSecret ? (
                <div className="flex gap-2">
                  <Input id="secret" value="•••••••• configured" disabled readOnly aria-label={t('secretConfigured')} />
                  <Button type="button" variant="outline" onClick={() => setReplacingSecret(true)}>
                    {t('replace')}
                  </Button>
                </div>
              ) : (
                <Input
                  id="secret"
                  type="password"
                  autoComplete="new-password"
                  placeholder={t('secretPlaceholder')}
                  {...register('secret')}
                />
              )}
            </Field>

            {/* Enable */}
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" className="h-4 w-4 accent-primary" {...register('isEnabled')} />
              {t('enabledLabel')}
            </label>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <TestEmail sendTest={sendTest} />
              <Button type="submit" disabled={busy}>
                {busy ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    {t('saving')}
                  </>
                ) : (
                  t('save')
                )}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

function StatusBar({ settings }: { settings: EmailSettings }) {
  const t = useTranslations('settings.email');
  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <Badge variant={settings.isEnabled ? 'default' : 'secondary'}>
        {settings.isEnabled ? t('status.enabled') : t('status.disabled')}
      </Badge>
      {settings.lastTestSucceeded === true && (
        <span className="flex items-center gap-1 text-muted-foreground">
          <CheckCircle2 className="h-4 w-4 text-primary" aria-hidden="true" />
          {t('status.lastTestPassed')}
        </span>
      )}
      {settings.lastTestSucceeded === false && (
        <span className="flex items-center gap-1 text-destructive">
          <XCircle className="h-4 w-4" aria-hidden="true" />
          {t('status.lastTestFailed')}
        </span>
      )}
    </div>
  );
}

function TestEmail({ sendTest }: { sendTest: ReturnType<typeof useSendTestEmail> }) {
  const t = useTranslations('settings.email');
  return (
    <div className="flex items-center gap-3">
      <Button
        type="button"
        variant="outline"
        disabled={sendTest.isPending}
        onClick={() => void sendTest.mutateAsync().catch(() => null)}
      >
        {sendTest.isPending ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('testing')}
          </>
        ) : (
          t('sendTest')
        )}
      </Button>
      {sendTest.isSuccess && <span role="status" className="text-sm text-muted-foreground">{t('testSent')}</span>}
      {sendTest.isError && <span role="alert" className="text-sm text-destructive">{t('testFailed')}</span>}
    </div>
  );
}

function Field({
  id,
  label,
  error,
  children,
}: {
  id: string;
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
