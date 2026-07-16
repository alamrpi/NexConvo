'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Card, CardContent } from '@/shared/ui/card';
import { Skeleton } from '@/shared/ui/skeleton';
import { useSessionStore } from '@/features/auth/model/session.store';
import { publicEnv } from '@/shared/lib/env';
import { chatSettingsSchema, type ChatSettingsValues } from '../model/chat-settings.schema';
import type { WorkspaceChatSettingsDto } from '../model/chat-settings.types';
import { useChatSettings } from '../api/use-chat-settings';
import { useUpdateChatSettings, ChatSettingsError } from '../api/use-update-chat-settings';

export function ChatWidgetSettingsForm() {
  const t = useTranslations('settings.channels.widget');
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useChatSettings();

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

  return <ChatWidgetFields settings={data} />;
}

function FormSkeleton() {
  return (
    <Card>
      <CardContent className="space-y-5 p-5">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="space-y-1.5">
            <Skeleton className="h-3.5 w-32" />
            <Skeleton className="h-9 w-full" />
          </div>
        ))}
        <Skeleton className="h-9 w-32" />
      </CardContent>
    </Card>
  );
}

function ChatWidgetFields({ settings }: { settings: WorkspaceChatSettingsDto }) {
  const t = useTranslations('settings.channels.widget');
  const update = useUpdateChatSettings();

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ChatSettingsValues>({
    resolver: zodResolver(chatSettingsSchema),
    mode: 'onTouched',
    defaultValues: {
      ...settings,
      widgetIconUrl: settings.widgetIconUrl || '',
      widgetPrimaryColor: settings.widgetPrimaryColor || '#0F172A',
      widgetSecondaryColor: settings.widgetSecondaryColor || '#3B82F6',
      widgetWelcomeMessage: settings.widgetWelcomeMessage || 'Hi there! How can I help you today?',
    },
  });

  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || update.isPending;

  const onSubmit = handleSubmit(async (values) => {
    // Copy so we never mutate RHF's live form state; normalize empty icon URL back to null.
    const payload = { ...values, widgetIconUrl: values.widgetIconUrl ? values.widgetIconUrl : null };
    // Swallow the rejection here so it doesn't bubble as an unhandled promise — the mutation's
    // error state (below) drives the UI, distinguishing a 409 conflict from a generic failure (S17).
    await update.mutateAsync(payload).catch(() => undefined);
  });

  // 409 optimistic-concurrency conflict gets its own message; anything else is generic (S17).
  const saveErrorText =
    update.error instanceof ChatSettingsError && update.error.code === 'conflict'
      ? t('conflict')
      : t('saveError');

  // Embed snippet: real widget-host origin + the tenant's unguessable WidgetToken — never localhost
  // and never the enumerable tenant id (audit M4/C2).
  const embedCode = `<script\n  src="${publicEnv.NEXT_PUBLIC_WIDGET_URL}/nexconvo-widget.js"\n  data-token="${settings.widgetToken}"\n  defer\n></script>`;

  return (
    <div className="space-y-6">
      <Card>
        <CardContent className="p-5">
          <form onSubmit={onSubmit} noValidate className="space-y-5">
            {update.isSuccess && (
              <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm">
                {t('saved')}
              </p>
            )}
            {update.isError && (
              <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {saveErrorText}
              </p>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="widgetPrimaryColor" label={t('color')} error={errorText(errors.widgetPrimaryColor?.message)}>
                <Input type="color" id="widgetPrimaryColor" {...register('widgetPrimaryColor')} aria-invalid={!!errors.widgetPrimaryColor} aria-describedby={errors.widgetPrimaryColor ? 'widgetPrimaryColor-error' : undefined} className="h-12 w-24 cursor-pointer p-1" />
              </Field>

              <Field id="widgetSecondaryColor" label={t('secondaryColor')} error={errorText(errors.widgetSecondaryColor?.message)}>
                <Input type="color" id="widgetSecondaryColor" {...register('widgetSecondaryColor')} aria-invalid={!!errors.widgetSecondaryColor} aria-describedby={errors.widgetSecondaryColor ? 'widgetSecondaryColor-error' : undefined} className="h-12 w-24 cursor-pointer p-1" />
              </Field>
            </div>

            <Field id="widgetIconUrl" label={t('iconUrl')} error={errorText(errors.widgetIconUrl?.message)}>
              <Input id="widgetIconUrl" placeholder={t('iconUrlPlaceholder')} {...register('widgetIconUrl')} aria-invalid={!!errors.widgetIconUrl} aria-describedby={errors.widgetIconUrl ? 'widgetIconUrl-error' : undefined} />
            </Field>

            <Field id="widgetWelcomeMessage" label={t('welcomeMessage')} error={errorText(errors.widgetWelcomeMessage?.message)}>
              <Input id="widgetWelcomeMessage" placeholder={t('welcomeMessagePlaceholder')} {...register('widgetWelcomeMessage')} aria-invalid={!!errors.widgetWelcomeMessage} aria-describedby={errors.widgetWelcomeMessage ? 'widgetWelcomeMessage-error' : undefined} />
            </Field>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-start pt-2">
              <Button type="submit" size="sm" disabled={busy}>
                {busy ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    {t('saving')}
                  </>
                ) : (
                  <>
                    <Save aria-hidden="true" />
                    {t('save')}
                  </>
                )}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-5 space-y-3">
          <div>
            <h3 className="text-sm font-semibold tracking-tight text-foreground">{t('embedTitle')}</h3>
            <p className="text-xs text-muted-foreground mt-0.5">{t('embedDescription')}</p>
          </div>
          <div className="relative">
            <pre className="rounded-lg bg-muted p-4 text-xs font-mono text-muted-foreground overflow-x-auto select-all border">
              {embedCode}
            </pre>
          </div>
        </CardContent>
      </Card>
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
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-xs font-medium">{label}</Label>
      {children}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
