'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Check,
  CheckCircle2,
  Copy,
  Eye,
  EyeOff,
  Loader2,
  MessageSquare,
  X,
  XCircle,
  Zap,
} from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Badge } from '@/shared/ui/badge';
import { Skeleton } from '@/shared/ui/skeleton';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/shared/ui/dialog';
import { cn } from '@/shared/lib/cn';
import { useSessionStore } from '@/features/auth/model/session.store';
import {
  saveChannelConnectionSchema,
  type SaveChannelConnectionValues,
} from '@/features/settings/model/channel-connection.schema';
import type {
  ChannelConnectionDto,
  ChannelType,
  ConnectionStatus,
} from '@/features/settings/model/channel-connection.types';
import { useChannelConnections } from '@/features/settings/api/use-channel-connections';
import { useSaveChannelConnection, ChannelConnectionError } from '@/features/settings/api/use-save-channel-connection';
import { useDeleteChannelConnection } from '@/features/settings/api/use-delete-channel-connection';
import { useTestChannelConnection } from '@/features/settings/api/use-test-channel-connection';
import { useRelativeTime } from '@/features/settings/components/health-badge';
import { useChatSettings } from '@/features/settings/api/use-chat-settings';
import { useUpdateChatSettings } from '@/features/settings/api/use-update-chat-settings';

// ── Static channel metadata (no server data — these are always the same 5) ──────

interface ChannelMeta {
  channel: ChannelType;
  bgClass: string;
  initial: string;
}

const CHANNEL_META: ChannelMeta[] = [
  { channel: 'whatsapp', bgClass: 'bg-chatChannel-whatsapp', initial: 'W' },
  { channel: 'facebook', bgClass: 'bg-chatChannel-facebook', initial: 'F' },
  { channel: 'instagram', bgClass: 'bg-chatChannel-instagram', initial: 'I' },
  { channel: 'telegram', bgClass: 'bg-chatChannel-telegram', initial: 'T' },
  { channel: 'web', bgClass: 'bg-chatChannel-web', initial: 'W' },
];

// ── Helpers ──────────────────────────────────────────────────────────────────────

function statusVariant(status: ConnectionStatus): string {
  if (status === 'connected') return 'border-chatConfidence-high/30 text-chatConfidence-high';
  if (status === 'error') return 'border-destructive/30 text-destructive';
  return 'text-muted-foreground';
}

// ── Loading skeleton ─────────────────────────────────────────────────────────────

function ChannelListSkeleton() {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-card">
      {Array.from({ length: 5 }).map((_, i) => (
        <div key={i}>
          {i > 0 && <div className="h-px bg-border" />}
          <div className="flex items-center gap-4 px-4 py-4">
            <Skeleton className="h-9 w-9 rounded-lg" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3.5 w-24" />
              <Skeleton className="h-3 w-40" />
            </div>
            <Skeleton className="hidden h-6 w-24 rounded-full sm:block" />
            <Skeleton className="h-8 w-20 rounded-md" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ── Step indicator ────────────────────────────────────────────────────────────────

function StepIndicator({ current, stepLabels }: { current: number; stepLabels: string[] }) {
  return (
    <div className="flex items-center" aria-label="Connection steps" role="list">
      {stepLabels.map((label, idx) => {
        const step = idx + 1;
        const isCompleted = step < current;
        const isCurrent = step === current;
        return (
          <React.Fragment key={label}>
            <div className="flex flex-col items-center gap-1" role="listitem">
              <div
                className={cn(
                  'flex h-7 w-7 items-center justify-center rounded-full border-2 text-xs font-semibold transition-colors',
                  isCompleted && 'border-primary bg-primary text-primary-foreground',
                  isCurrent && 'border-primary bg-background text-primary',
                  !isCompleted && !isCurrent && 'border-border bg-background text-muted-foreground',
                )}
                aria-current={isCurrent ? 'step' : undefined}
              >
                {isCompleted ? <Check className="h-3.5 w-3.5" aria-hidden /> : step}
              </div>
              <span
                className={cn(
                  'hidden text-[10px] sm:block',
                  isCurrent ? 'font-medium text-foreground' : 'text-muted-foreground',
                )}
              >
                {label}
              </span>
            </div>
            {idx < stepLabels.length - 1 && (
              <div
                className={cn('mx-1 mb-4 h-0.5 flex-1', step < current ? 'bg-primary' : 'bg-border')}
                aria-hidden
              />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// ── Masked input (password with show/hide) ────────────────────────────────────────

function MaskedInput(props: React.ComponentProps<typeof Input> & { 'aria-label': string }) {
  const [visible, setVisible] = React.useState(false);
  return (
    <div className="relative">
      <Input {...props} type={visible ? 'text' : 'password'} className={cn('pr-10', props.className)} />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        aria-label={visible ? 'Hide value' : 'Show value'}
        className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {visible ? <EyeOff className="h-4 w-4" aria-hidden /> : <Eye className="h-4 w-4" aria-hidden />}
      </button>
    </div>
  );
}

// ── Field helper component ────────────────────────────────────────────────────────

function FormField({
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
      <Label htmlFor={id} className="text-sm">
        {label}
      </Label>
      {children}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

// ── Web widget configure panel ────────────────────────────────────────────────────

type WidgetPosition = 'bottom-right' | 'bottom-left';

function WebWidgetConfig({ connectionId }: { connectionId: string }) {
  const t = useTranslations('settings.channels.widget');
  const user = useSessionStore((s) => s.user);
  const currentTenantId = user?.tenantId || 'YOUR_WORKSPACE_ID';

  const { data: settings, isLoading, isError } = useChatSettings();
  const update = useUpdateChatSettings();

  const [color, setColor] = React.useState('#6366f1');
  const [welcomeMsg, setWelcomeMsg] = React.useState('');
  const [position, setPosition] = React.useState<WidgetPosition>('bottom-right');

  const [saved, setSaved] = React.useState(false);
  const [copied, setCopied] = React.useState(false);

  const embedCode = `<script\n  src="http://localhost:5173/src/main.tsx"\n  data-tenant="${currentTenantId}"\n  defer\n></script>`;

  // Initialize values when settings load
  React.useEffect(() => {
    if (settings) {
      setColor(settings.widgetPrimaryColor || '#6366f1');
      setWelcomeMsg(settings.widgetWelcomeMessage || 'Hi there! How can I help you today?');
    }
  }, [settings]);
  async function handleSave() {
    if (!settings) return;
    setSaved(false);
    try {
      await update.mutateAsync({
        ...settings,
        widgetPrimaryColor: color || '#6366f1',
        widgetSecondaryColor: settings.widgetSecondaryColor || '#3B82F6',
        widgetWelcomeMessage: welcomeMsg || 'Hi there! How can I help you today?',
        widgetIconUrl: settings.widgetIconUrl ? settings.widgetIconUrl : null,
      });
      setSaved(true);
    } catch (e) {
      console.error(e);
    }
  }
  function handleCopy() {
    navigator.clipboard.writeText(embedCode);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  // Suppress unused variable warning for connectionId until backend wires up
  void connectionId;

  if (isLoading) {
    return (
      <div className="space-y-4 p-4">
        <Skeleton className="h-6 w-32" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    );
  }

  if (isError || !settings) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        Couldn&apos;t load widget settings. Please try again.
      </p>
    );
  }

  return (
    <div className="space-y-6">
      <div className="rounded-lg border border-border bg-muted/40 p-4">
        <h3 className="mb-4 text-sm font-semibold text-foreground">{t('title')}</h3>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="widget-color">{t('color')}</Label>
              <div className="flex items-center gap-2">
                <input
                  id="widget-color"
                  type="color"
                  value={color}
                  onChange={(e) => setColor(e.target.value)}
                  className="h-8 w-12 cursor-pointer rounded border border-border p-0.5"
                  aria-label="Widget accent color"
                />
                <span className="text-sm text-muted-foreground">{color}</span>
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="widget-welcome">{t('welcomeMessage')}</Label>
              <Input
                id="widget-welcome"
                placeholder={t('welcomeMessagePlaceholder')}
                value={welcomeMsg}
                onChange={(e) => setWelcomeMsg(e.target.value)}
              />
            </div>

            <div className="space-y-1.5">
              <Label>{t('position')}</Label>
              <div className="flex gap-2">
                {(
                  [
                    { value: 'bottom-right' as WidgetPosition, labelKey: 'positionBottomRight' },
                    { value: 'bottom-left' as WidgetPosition, labelKey: 'positionBottomLeft' },
                  ] as const
                ).map(({ value, labelKey }) => (
                  <button
                    key={value}
                    type="button"
                    onClick={() => setPosition(value)}
                    aria-pressed={position === value}
                    className={cn(
                      'flex-1 rounded-md border px-3 py-2 text-xs font-medium transition-colors',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                      position === value
                        ? 'border-primary bg-primary/10 text-primary'
                        : 'border-border bg-background text-muted-foreground hover:border-ring/50 hover:text-foreground',
                    )}
                  >
                    {t(labelKey)}
                  </button>
                ))}
              </div>
            </div>

            <div className="flex items-center gap-3 pt-1">
              <Button size="sm" onClick={handleSave} disabled={update.isPending}>
                {update.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" aria-hidden />
                    {t('saving')}
                  </>
                ) : (
                  t('save')
                )}
              </Button>
              {saved && (
                <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                  <Check className="h-3 w-3 text-chatConfidence-high" aria-hidden />
                  {t('saved')}
                </span>
              )}
            </div>
          </div>

          {/* Live preview */}
          <div className="space-y-1.5">
            <Label>{t('preview')}</Label>
            <div className="overflow-hidden rounded-lg border border-border bg-background">
              <div className="flex items-center gap-1.5 border-b border-border bg-muted/60 px-3 py-2" aria-hidden>
                <span className="h-2.5 w-2.5 rounded-full bg-red-400" />
                <span className="h-2.5 w-2.5 rounded-full bg-yellow-400" />
                <span className="h-2.5 w-2.5 rounded-full bg-green-400" />
              </div>
              <div className="relative h-48">
                <div
                  className={cn(
                    'absolute bottom-3 flex h-11 w-11 items-center justify-center rounded-full shadow-md transition-transform',
                    position === 'bottom-right' ? 'right-3' : 'left-3',
                  )}
                  style={{ backgroundColor: color }}
                  aria-label="Chat bubble preview"
                >
                  <MessageSquare className="h-5 w-5 text-white" aria-hidden />
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Embed Guide */}
      <div className="rounded-lg border border-border bg-card p-5 space-y-3">
        <div>
          <h4 className="text-sm font-semibold text-foreground">How to Embed</h4>
          <p className="text-xs text-muted-foreground mt-0.5">
            Copy and paste this script tag into the HTML of your website (e.g. right before the closing &lt;/body&gt; tag).
          </p>
        </div>
        <div className="relative">
          <pre className="rounded-lg bg-muted p-4 pr-20 text-xs font-mono text-muted-foreground overflow-x-auto select-all border">
            {embedCode}
          </pre>
          <Button
            size="sm"
            variant="outline"
            className="absolute right-2 top-2 h-7 px-2"
            onClick={handleCopy}
          >
            {copied ? (
              <>
                <Check className="h-3 w-3 mr-1 text-chatConfidence-high" />
                Copied
              </>
            ) : (
              <>
                <Copy className="h-3 w-3 mr-1" />
                Copy
              </>
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}

// ── Connect drawer (right-side panel) ────────────────────────────────────────────

type VerifyState = 'idle' | 'loading' | 'success' | 'failed';

interface ConnectDrawerProps {
  channel: ChannelType;
  channelMeta: ChannelMeta;
  existingConnection: ChannelConnectionDto | null;
  onClose: () => void;
}

function ConnectDrawer({ channel, channelMeta, existingConnection, onClose }: ConnectDrawerProps) {
  const t = useTranslations('settings.channels');
  const tDrawer = useTranslations('settings.channels.drawer');
  const tErrors = useTranslations('settings.channels.errors');

  const isWeb = channel === 'web';
  const isConfigureMode = existingConnection !== null;

  const [step, setStep] = React.useState(1);
  const [verifyState, setVerifyState] = React.useState<VerifyState>('idle');
  const [verifyAccountName, setVerifyAccountName] = React.useState<string | undefined>();
  const [verifyError, setVerifyError] = React.useState<string | undefined>();
  const [copied, setCopied] = React.useState(false);
  const [savedConnectionId, setSavedConnectionId] = React.useState<string | undefined>(
    existingConnection?.id,
  );

  const save = useSaveChannelConnection();
  const testConn = useTestChannelConnection();

  const stepLabels = [
    tDrawer('steps.instructions'),
    tDrawer('steps.credentials'),
    tDrawer('steps.verify'),
    tDrawer('steps.webhook'),
    tDrawer('steps.done'),
  ];

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SaveChannelConnectionValues>({
    resolver: zodResolver(saveChannelConnectionSchema),
    mode: 'onTouched',
    defaultValues: {
      channel,
      displayName: existingConnection?.displayName ?? '',
      externalAccountId: existingConnection?.externalAccountId ?? '',
      accessToken: '',
      appSecret: '',
      verifyToken: '',
    },
  });

  const webhookUrl = savedConnectionId
    ? `https://api.nexconvo.app/webhooks/${channel}/${savedConnectionId}`
    : `https://api.nexconvo.app/webhooks/${channel}/pending`;

  async function handleCopy() {
    await navigator.clipboard.writeText(webhookUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  async function handleVerify() {
    if (!savedConnectionId) return;
    setVerifyState('loading');
    setVerifyError(undefined);
    try {
      const result = await testConn.mutateAsync(savedConnectionId);
      if (result.success) {
        setVerifyState('success');
        setVerifyAccountName(result.detail ?? undefined);
      } else {
        setVerifyState('failed');
        setVerifyError(result.errorMessage ?? tErrors('generic'));
      }
    } catch {
      setVerifyState('failed');
      setVerifyError(tErrors('generic'));
    }
  }

  const onSubmitCredentials = handleSubmit(async (values) => {
    const result = await save.mutateAsync(values).catch(() => null);
    if (result) {
      setSavedConnectionId(result.id);
      setStep(3);
    }
  });

  const isConflict = save.error instanceof ChannelConnectionError && save.error.code === 'conflict';
  const isTestFailed = save.error instanceof ChannelConnectionError && save.error.code === 'test-failed';

  const titleKey = isConfigureMode ? 'configureTitle' : 'connectTitle';
  const channelName = t(`channels.${channel}`);

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/50"
        onClick={onClose}
        aria-hidden="true"
      />
      {/* Slide-in panel — ≤300ms transform animation (S26) */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label={tDrawer(titleKey, { name: channelName })}
        className="animate-in slide-in-from-right duration-200 fixed inset-y-0 right-0 z-50 flex w-full flex-col bg-card shadow-xl sm:w-[420px]"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border px-4 py-3">
          <div className="flex items-center gap-2">
            <div
              className={cn(
                'flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-sm font-bold text-white',
                channelMeta.bgClass,
              )}
              aria-hidden="true"
            >
              {channelMeta.initial}
            </div>
            <span className="font-semibold text-foreground">
              {tDrawer(titleKey, { name: channelName })}
            </span>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={tDrawer('close')}
            className="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <X className="h-4 w-4" aria-hidden />
          </button>
        </div>

        {/* Step indicator */}
        <div className="border-b border-border px-4 py-3">
          <StepIndicator current={step} stepLabels={stepLabels} />
        </div>

        {/* Step content */}
        <div className="flex-1 overflow-y-auto p-5">
          {/* Step 1 — Instructions */}
          {step === 1 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {tDrawer('steps.instructions')}
              </h2>
              <p className="text-sm leading-relaxed text-muted-foreground">
                {tDrawer(`instructions.${channel}`)}
              </p>
            </div>
          )}

          {/* Step 2 — Credentials form */}
          {step === 2 && (
            <form id="credentials-form" onSubmit={onSubmitCredentials} noValidate className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {tDrawer('steps.credentials')}
              </h2>

              {save.isError && (
                <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  {isConflict
                    ? tErrors('conflict')
                    : isTestFailed
                      ? tErrors('credentialsExpired')
                      : tErrors('generic')}
                </p>
              )}

              <input type="hidden" {...register('channel')} value={channel} />

              <FormField
                id="displayName"
                label={tDrawer('fields.displayName')}
                error={errors.displayName?.message ? tErrors(errors.displayName.message as 'displayNameRequired') : undefined}
              >
                <Input
                  id="displayName"
                  placeholder={tDrawer('fields.displayNamePlaceholder')}
                  aria-invalid={!!errors.displayName}
                  aria-describedby={errors.displayName ? 'displayName-error' : undefined}
                  {...register('displayName')}
                />
              </FormField>

              {(channel === 'whatsapp') && (
                <FormField
                  id="externalAccountId"
                  label={tDrawer('fields.phoneNumberId')}
                  error={errors.externalAccountId?.message ? tErrors(errors.externalAccountId.message as 'externalAccountIdRequired') : undefined}
                >
                  <Input
                    id="externalAccountId"
                    placeholder={tDrawer('fields.phoneNumberIdPlaceholder')}
                    aria-invalid={!!errors.externalAccountId}
                    aria-describedby={errors.externalAccountId ? 'externalAccountId-error' : undefined}
                    {...register('externalAccountId')}
                  />
                </FormField>
              )}

              {(channel === 'facebook' || channel === 'instagram') && (
                <FormField
                  id="externalAccountId"
                  label={tDrawer('fields.pageId')}
                  error={errors.externalAccountId?.message ? tErrors(errors.externalAccountId.message as 'externalAccountIdRequired') : undefined}
                >
                  <Input
                    id="externalAccountId"
                    placeholder={tDrawer('fields.pageIdPlaceholder')}
                    aria-invalid={!!errors.externalAccountId}
                    aria-describedby={errors.externalAccountId ? 'externalAccountId-error' : undefined}
                    {...register('externalAccountId')}
                  />
                </FormField>
              )}

              {channel === 'telegram' && (
                <FormField
                  id="externalAccountId"
                  label={tDrawer('fields.botUsername')}
                  error={errors.externalAccountId?.message ? tErrors(errors.externalAccountId.message as 'externalAccountIdRequired') : undefined}
                >
                  <Input
                    id="externalAccountId"
                    placeholder={tDrawer('fields.botUsernamePlaceholder')}
                    aria-invalid={!!errors.externalAccountId}
                    aria-describedby={errors.externalAccountId ? 'externalAccountId-error' : undefined}
                    {...register('externalAccountId')}
                  />
                </FormField>
              )}

              {channel === 'telegram' && (
                <FormField
                  id="accessToken"
                  label={tDrawer('fields.botToken')}
                  error={errors.accessToken?.message ? tErrors(errors.accessToken.message as 'accessTokenRequired') : undefined}
                >
                  <MaskedInput
                    id="accessToken"
                    aria-label={tDrawer('fields.botToken')}
                    placeholder={tDrawer('fields.botTokenPlaceholder')}
                    aria-invalid={!!errors.accessToken}
                    aria-describedby={errors.accessToken ? 'accessToken-error' : undefined}
                    {...register('accessToken')}
                  />
                </FormField>
              )}

              {(channel === 'whatsapp' || channel === 'facebook' || channel === 'instagram') && (
                <>
                  <FormField
                    id="accessToken"
                    label={tDrawer('fields.accessToken')}
                    error={errors.accessToken?.message ? tErrors(errors.accessToken.message as 'accessTokenRequired') : undefined}
                  >
                    <MaskedInput
                      id="accessToken"
                      aria-label={tDrawer('fields.accessToken')}
                      placeholder={tDrawer('fields.accessTokenPlaceholder')}
                      aria-invalid={!!errors.accessToken}
                      aria-describedby={errors.accessToken ? 'accessToken-error' : undefined}
                      {...register('accessToken')}
                    />
                  </FormField>
                  <FormField
                    id="appSecret"
                    label={tDrawer('fields.appSecret')}
                    error={errors.appSecret?.message ? tErrors(errors.appSecret.message as 'appSecretRequired') : undefined}
                  >
                    <MaskedInput
                      id="appSecret"
                      aria-label={tDrawer('fields.appSecret')}
                      placeholder={tDrawer('fields.appSecretPlaceholder')}
                      aria-invalid={!!errors.appSecret}
                      aria-describedby={errors.appSecret ? 'appSecret-error' : undefined}
                      {...register('appSecret')}
                    />
                  </FormField>
                </>
              )}
            </form>
          )}

          {/* Step 3 — Verify */}
          {step === 3 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {tDrawer('steps.verify')}
              </h2>
              <p className="text-sm text-muted-foreground">
                {tDrawer('verify.description', { name: channelName })}
              </p>

              {isWeb ? (
                // Web widget doesn't need credential verification
                <div className="flex items-center gap-2 rounded-md border border-chatConfidence-high/30 bg-chatConfidence-high/10 px-3 py-2 text-sm text-chatConfidence-high">
                  <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden />
                  {tDrawer('verify.success', { accountName: 'Web Widget' })}
                </div>
              ) : (
                <>
                  <Button
                    onClick={handleVerify}
                    disabled={verifyState === 'loading' || !savedConnectionId}
                    className="w-full"
                  >
                    {verifyState === 'loading' ? (
                      <>
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />
                        {tDrawer('verify.verifying')}
                      </>
                    ) : (
                      tDrawer('verify.button')
                    )}
                  </Button>

                  {verifyState === 'success' && (
                    <div className="flex items-center gap-2 rounded-md border border-chatConfidence-high/30 bg-chatConfidence-high/10 px-3 py-2 text-sm text-chatConfidence-high">
                      <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden />
                      {tDrawer('verify.success', { accountName: verifyAccountName ?? channelName })}
                    </div>
                  )}

                  {verifyState === 'failed' && (
                    <div className="space-y-1">
                      <div className="flex items-center gap-2 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                        <XCircle className="h-4 w-4 shrink-0" aria-hidden />
                        {tDrawer('verify.failed')}
                      </div>
                      {verifyError && (
                        <p className="text-xs text-destructive">{verifyError}</p>
                      )}
                    </div>
                  )}
                </>
              )}
            </div>
          )}

          {/* Step 4 — Webhook */}
          {step === 4 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {tDrawer('steps.webhook')}
              </h2>
              <p className="text-sm text-muted-foreground">
                {tDrawer('webhook.description', { name: channelName })}
              </p>
              <div className="rounded-md border border-border bg-muted p-3">
                <code className="break-all text-xs text-foreground">{webhookUrl}</code>
              </div>
              <Button variant="outline" onClick={handleCopy} className="w-full gap-2">
                {copied ? (
                  <>
                    <Check className="h-4 w-4 text-chatConfidence-high" aria-hidden />
                    {tDrawer('webhook.copied')}
                  </>
                ) : (
                  <>
                    <Copy className="h-4 w-4" aria-hidden />
                    {tDrawer('webhook.copy')}
                  </>
                )}
              </Button>
            </div>
          )}

          {/* Step 5 — Done */}
          {step === 5 && (
            <div className="flex flex-col items-center gap-4 py-8 text-center">
              <div className="flex h-16 w-16 items-center justify-center rounded-full bg-chatConfidence-high/15">
                <CheckCircle2 className="h-8 w-8 text-chatConfidence-high" aria-hidden />
              </div>
              <p className="text-lg font-semibold text-foreground">{tDrawer('done.title')}</p>
              <p className="text-sm text-muted-foreground">
                {tDrawer('done.description', { name: channelName })}
              </p>
            </div>
          )}
        </div>

        {/* Footer navigation */}
        <div className="border-t border-border px-4 py-3">
          <div className="flex gap-2">
            {step > 1 && step < 5 && (
              <Button variant="outline" onClick={() => setStep((s) => s - 1)} className="flex-1">
                {tDrawer('back')}
              </Button>
            )}

            {step === 1 && (
              <Button onClick={() => setStep(2)} className="w-full">
                {tDrawer('next')}
              </Button>
            )}

            {step === 2 && (
              <Button
                type="submit"
                form="credentials-form"
                disabled={save.isPending}
                className="flex-1"
              >
                {save.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />
                    {tDrawer('next')}
                  </>
                ) : (
                  tDrawer('next')
                )}
              </Button>
            )}

            {step === 3 && (
              <Button
                onClick={() => setStep(4)}
                disabled={!isWeb && verifyState !== 'success'}
                className="flex-1"
              >
                {tDrawer('next')}
              </Button>
            )}

            {step === 4 && (
              <Button onClick={() => setStep(5)} className="flex-1">
                {tDrawer('next')}
              </Button>
            )}

            {step === 5 && (
              <Button onClick={onClose} className="w-full">
                {tDrawer('finish')}
              </Button>
            )}
          </div>
        </div>
      </div>
    </>
  );
}

// ── Channel row ───────────────────────────────────────────────────────────────────

interface ChannelRowProps {
  meta: ChannelMeta;
  connection: ChannelConnectionDto | undefined;
  onConnect: () => void;
  onConfigure: () => void;
  isWidgetExpanded: boolean;
  onWidgetToggle: () => void;
}

function ChannelRow({
  meta,
  connection,
  onConnect,
  onConfigure,
  isWidgetExpanded,
  onWidgetToggle,
}: ChannelRowProps) {
  const t = useTranslations('settings.channels');
  const deleteConn = useDeleteChannelConnection();
  const isWeb = meta.channel === 'web';

  const status: ConnectionStatus = connection?.status ?? 'disconnected';
  const relativeTested = useRelativeTime(connection?.lastTestedAt ?? null);
  const lastTestedLabel = relativeTested ? t('lastTested', { time: relativeTested }) : t('lastTestedNever');

  function handleActionClick() {
    if (isWeb && connection?.status === 'connected') {
      onWidgetToggle();
    } else if (connection) {
      onConfigure();
    } else {
      onConnect();
    }
  }

  function handleDisconnect() {
    if (connection) {
      deleteConn.mutate(connection.id);
    }
  }

  return (
    <div>
      <div className="flex items-center gap-4 px-4 py-3.5">
        {/* Channel icon */}
        <div
          className={cn(
            'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-sm font-bold text-white',
            meta.bgClass,
          )}
          aria-hidden="true"
        >
          {meta.initial}
        </div>

        {/* Name + description */}
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-foreground">{t(`channels.${meta.channel}`)}</p>
          <p className="text-xs text-muted-foreground">
            {connection?.displayName
              ? connection.displayName
              : t(`descriptions.${meta.channel}`)}
          </p>
          {connection && <p className="text-xs text-muted-foreground">{lastTestedLabel}</p>}
        </div>

        {/* Status badge (hidden on smallest screens) */}
        <div className="hidden shrink-0 sm:block">
          {status === 'connected' && (
            <Badge variant="outline" className={statusVariant('connected')}>
              <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-chatConfidence-high" aria-hidden />
              {t('status.connected')}
            </Badge>
          )}
          {status === 'error' && (
            <Badge variant="outline" className={statusVariant('error')}>
              <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-destructive" aria-hidden />
              {t('status.error')}
            </Badge>
          )}
          {status === 'disconnected' && (
            <Badge variant="outline" className={statusVariant('disconnected')}>
              {t('status.disconnected')}
            </Badge>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex shrink-0 items-center gap-2">
          {status === 'disconnected' && (
            <Button onClick={handleActionClick} size="sm" className="h-8 px-4">
              <Zap className="mr-1.5 h-3.5 w-3.5" aria-hidden />
              {t('connect')}
            </Button>
          )}
          {status === 'connected' && (
            <>
              <Button onClick={handleActionClick} size="sm" variant="outline" className="h-8 px-3">
                {t('configure')}
              </Button>
              <Button
                onClick={handleDisconnect}
                size="sm"
                variant="ghost"
                disabled={deleteConn.isPending}
                className="h-8 px-3 text-muted-foreground hover:bg-destructive/10 hover:text-destructive disabled:opacity-50"
                aria-label={`${t('disconnect')} ${t(`channels.${meta.channel}`)}`}
              >
                {deleteConn.isPending ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
                ) : (
                  t('disconnect')
                )}
              </Button>
            </>
          )}
          {status === 'error' && (
            <>
              <Button onClick={handleActionClick} size="sm" variant="destructive" className="h-8 px-3">
                {t('reconnect')}
              </Button>
              <Button
                onClick={handleDisconnect}
                size="sm"
                variant="ghost"
                disabled={deleteConn.isPending}
                className="h-8 px-3 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              >
                {deleteConn.isPending ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
                ) : (
                  t('disconnect')
                )}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Inline error message for errored connections */}
      {status === 'error' && connection?.errorMessage && (
        <p className="px-4 pb-3 pl-[3.25rem] text-xs text-destructive">
          {connection.errorMessage}
        </p>
      )}

      {/* Inline error message for errored connections */}
      {status === 'error' && connection?.errorMessage && (
        <p className="px-4 pb-3 pl-[3.25rem] text-xs text-destructive">
          {connection.errorMessage}
        </p>
      )}
    </div>
  );
}

// ── Shared page component ─────────────────────────────────────────────────────────

export function ChannelConnectionsPage() {
  const t = useTranslations('settings.channels');
  const { data, isLoading, isError } = useChannelConnections();

  const [activeChannel, setActiveChannel] = React.useState<{
    channel: ChannelType;
    meta: ChannelMeta;
    connection: ChannelConnectionDto | null;
  } | null>(null);

  const [configureWidgetId, setConfigureWidgetId] = React.useState<string | null>(null);

  function openDrawer(meta: ChannelMeta, connection: ChannelConnectionDto | null) {
    setActiveChannel({ channel: meta.channel, meta, connection });
  }

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div>
        <h1 className="text-lg font-medium text-foreground">{t('title')}</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">{t('description')}</p>
      </div>

      {isLoading && <ChannelListSkeleton />}

      {isError && (
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {t('loadError')}
        </p>
      )}

      {!isLoading && !isError && (
        <div className="overflow-hidden rounded-lg border border-border bg-card">
          {CHANNEL_META.map((meta, idx) => {
            const connection = data?.find((c) => c.channel === meta.channel) ?? undefined;
            return (
              <div key={meta.channel}>
                {idx > 0 && <div className="h-px bg-border" aria-hidden />}
                <ChannelRow
                  meta={meta}
                  connection={connection}
                  onConnect={() => openDrawer(meta, null)}
                  onConfigure={() => openDrawer(meta, connection ?? null)}
                  isWidgetExpanded={false}
                  onWidgetToggle={() => setConfigureWidgetId(connection?.id ?? null)}
                />
              </div>
            );
          })}
        </div>
      )}

      {/* Connect/configure drawer */}
      {activeChannel && (
        <ConnectDrawer
          channel={activeChannel.channel}
          channelMeta={activeChannel.meta}
          existingConnection={activeChannel.connection}
          onClose={() => setActiveChannel(null)}
        />
      )}

      {/* Configure Widget Modal */}
      <Dialog open={!!configureWidgetId} onOpenChange={(open) => !open && setConfigureWidgetId(null)}>
        <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t('widget.title')}</DialogTitle>
          </DialogHeader>
          {configureWidgetId && <WebWidgetConfig connectionId={configureWidgetId} />}
        </DialogContent>
      </Dialog>
    </div>
  );
}
