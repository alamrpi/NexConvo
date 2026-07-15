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
import { chatSettingsSchema, type ChatSettingsValues } from '../model/chat-settings.schema';
import type { WorkspaceChatSettingsDto } from '../model/chat-settings.types';
import { useChatSettings } from '../api/use-chat-settings';
import { useUpdateChatSettings } from '../api/use-update-chat-settings';

export function ChatWidgetSettingsForm() {
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useChatSettings();

  if (!canManage) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        You do not have permission to manage these settings.
      </p>
    );
  }

  if (isLoading) {
    return <FormSkeleton />;
  }

  if (isError || !data) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        Couldn&apos;t load widget settings. Please try again.
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
    // Convert empty string back to null for URL
    if (!values.widgetIconUrl) values.widgetIconUrl = null;
    await update.mutateAsync(values).catch(() => null);
  });

  const user = useSessionStore((s) => s.user);
  const currentTenantId = user?.tenantId || 'YOUR_WORKSPACE_ID';
  const embedCode = `<script\n  src="http://localhost:5173/src/main.tsx"\n  data-tenant="${currentTenantId}"\n  defer\n></script>`;

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
                Couldn&apos;t save widget settings. Please try again.
              </p>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <Field id="widgetPrimaryColor" label={t('color')} error={errorText(errors.widgetPrimaryColor?.message)}>
                <Input type="color" id="widgetPrimaryColor" {...register('widgetPrimaryColor')} aria-invalid={!!errors.widgetPrimaryColor} className="h-12 w-24 cursor-pointer p-1" />
              </Field>
              
              <Field id="widgetSecondaryColor" label="Secondary Color" error={errorText(errors.widgetSecondaryColor?.message)}>
                <Input type="color" id="widgetSecondaryColor" {...register('widgetSecondaryColor')} aria-invalid={!!errors.widgetSecondaryColor} className="h-12 w-24 cursor-pointer p-1" />
              </Field>
            </div>

            <Field id="widgetIconUrl" label="Widget Icon URL (Optional)" error={errorText(errors.widgetIconUrl?.message)}>
              <Input id="widgetIconUrl" placeholder="https://example.com/icon.png" {...register('widgetIconUrl')} aria-invalid={!!errors.widgetIconUrl} />
            </Field>

            <Field id="widgetWelcomeMessage" label={t('welcomeMessage')} error={errorText(errors.widgetWelcomeMessage?.message)}>
              <Input id="widgetWelcomeMessage" placeholder={t('welcomeMessagePlaceholder')} {...register('widgetWelcomeMessage')} aria-invalid={!!errors.widgetWelcomeMessage} />
            </Field>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-start pt-2">
              <Button type="submit" size="sm" disabled={busy}>
                {busy ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
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
            <h3 className="text-sm font-semibold tracking-tight text-foreground">How to Embed</h3>
            <p className="text-xs text-muted-foreground mt-0.5">
              Copy and paste this script tag into the HTML of your website (e.g., right before the closing &lt;/body&gt; tag).
            </p>
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
