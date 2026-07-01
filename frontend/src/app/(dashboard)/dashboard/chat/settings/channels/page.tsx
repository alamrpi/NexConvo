'use client';

import * as React from 'react';
import { useTranslations } from 'next-intl';
import {
  CheckCircle2,
  XCircle,
  Eye,
  EyeOff,
  X,
  Check,
  Copy,
  MessageSquare,
} from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Badge } from '@/shared/ui/badge';
import { Label } from '@/shared/ui/label';
import { cn } from '@/shared/lib/cn';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type ChannelStatus = 'connected' | 'not_connected' | 'error';

interface Credential {
  label: string;
  placeholder: string;
  masked?: boolean;
}

interface Channel {
  id: string;
  name: string;
  description: string;
  bgClass: string;
  initial: string;
  status: ChannelStatus;
  handle?: string;
  errorMsg?: string;
  credentials: Credential[];
}

// ---------------------------------------------------------------------------
// Mock data
// ---------------------------------------------------------------------------

const INITIAL_CHANNELS: Channel[] = [
  {
    id: 'whatsapp',
    name: 'WhatsApp',
    description: 'Connect via WhatsApp Business API',
    bgClass: 'bg-chatChannel-whatsapp',
    initial: 'W',
    status: 'connected',
    handle: '+88017-NexConvo',
    credentials: [
      { label: 'Business Account ID', placeholder: 'e.g. 1234567890' },
      { label: 'API Key', placeholder: 'Enter API key', masked: true },
      { label: 'Phone Number ID', placeholder: 'e.g. 9876543210' },
    ],
  },
  {
    id: 'facebook',
    name: 'Facebook',
    description: 'Connect your Facebook Page',
    bgClass: 'bg-chatChannel-facebook',
    initial: 'F',
    status: 'not_connected',
    handle: undefined,
    credentials: [
      { label: 'Page ID', placeholder: 'e.g. 123456789' },
      { label: 'Access Token', placeholder: 'Enter access token', masked: true },
    ],
  },
  {
    id: 'instagram',
    name: 'Instagram',
    description: 'Connect your Instagram Business account',
    bgClass: 'bg-chatChannel-instagram',
    initial: 'I',
    status: 'error',
    handle: '@nexconvo_ig',
    errorMsg: 'Token expired — please reconnect',
    credentials: [
      { label: 'Instagram Account ID', placeholder: 'e.g. 987654321' },
      { label: 'Access Token', placeholder: 'Enter access token', masked: true },
    ],
  },
  {
    id: 'telegram',
    name: 'Telegram',
    description: 'Connect your Telegram Bot',
    bgClass: 'bg-chatChannel-telegram',
    initial: 'T',
    status: 'not_connected',
    handle: undefined,
    credentials: [
      { label: 'Bot Token', placeholder: 'e.g. 123456:ABC-DEF', masked: true },
    ],
  },
  {
    id: 'web',
    name: 'Web Widget',
    description: 'Embed a chat widget on your website',
    bgClass: 'bg-chatChannel-web',
    initial: 'W',
    status: 'connected',
    handle: 'nexconvo.app/widget/dhaka-retail',
    credentials: [],
  },
];

// ---------------------------------------------------------------------------
// Step indicator
// ---------------------------------------------------------------------------

const STEP_LABELS = ['Credentials', 'Verify', 'Webhook', 'Test', 'Done'];

function StepIndicator({ current }: { current: number }) {
  return (
    <div className="flex items-center gap-0" aria-label="Connection steps">
      {STEP_LABELS.map((label, idx) => {
        const step = idx + 1;
        const isCompleted = step < current;
        const isCurrent = step === current;
        return (
          <React.Fragment key={label}>
            <div className="flex flex-col items-center gap-1">
              <div
                className={cn(
                  'flex h-7 w-7 items-center justify-center rounded-full border-2 text-xs font-semibold transition-colors',
                  isCompleted &&
                    'border-primary bg-primary text-primary-foreground',
                  isCurrent &&
                    'border-primary bg-background text-primary',
                  !isCompleted &&
                    !isCurrent &&
                    'border-border bg-background text-muted-foreground',
                )}
                aria-current={isCurrent ? 'step' : undefined}
              >
                {isCompleted ? <Check className="h-3.5 w-3.5" /> : step}
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
            {idx < STEP_LABELS.length - 1 && (
              <div
                className={cn(
                  'mx-1 mb-4 h-0.5 flex-1',
                  step < current ? 'bg-primary' : 'bg-border',
                )}
              />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Masked input with show/hide toggle
// ---------------------------------------------------------------------------

function MaskedInput(props: React.ComponentProps<typeof Input>) {
  const [visible, setVisible] = React.useState(false);
  return (
    <div className="relative">
      <Input {...props} type={visible ? 'text' : 'password'} className="pr-10" />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        aria-label={visible ? 'Hide value' : 'Show value'}
        className="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {visible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
      </button>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Connect drawer (right-side panel)
// ---------------------------------------------------------------------------

type VerifyState = 'idle' | 'loading' | 'success' | 'failure';
type TestState = 'idle' | 'loading' | 'success' | 'failure' | 'done';

interface ConnectDrawerProps {
  channel: Channel;
  onClose: () => void;
  onConnected: (channelId: string) => void;
}

function ConnectDrawer({ channel, onClose, onConnected }: ConnectDrawerProps) {
  const t = useTranslations('chat.channels');
  const [step, setStep] = React.useState(1);
  const [fieldValues, setFieldValues] = React.useState<Record<string, string>>({});
  const [verifyState, setVerifyState] = React.useState<VerifyState>('idle');
  const [testState, setTestState] = React.useState<TestState>('idle');
  const [copied, setCopied] = React.useState(false);

  const webhookUrl = `https://api.nexconvo.app/webhooks/${channel.id}/abc123`;

  function handleFieldChange(label: string, value: string) {
    setFieldValues((prev) => ({ ...prev, [label]: value }));
  }

  async function handleVerify() {
    setVerifyState('loading');
    await new Promise((r) => setTimeout(r, 1500));
    // Fix: always succeed for demo UX
    setVerifyState('success');
  }

  async function handleCopy() {
    await navigator.clipboard.writeText(webhookUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  async function handleSendTest() {
    setTestState('loading');
    await new Promise((r) => setTimeout(r, 1500));
    // Fix: always succeed for demo UX
    setTestState('success');
    setTimeout(() => setTestState('done'), 100);
  }

  function handleDone() {
    onConnected(channel.id);
    onClose();
  }

  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/50"
        onClick={onClose}
        aria-hidden="true"
      />
      {/* Panel with slide-in animation */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label={`Connect ${channel.name}`}
        className="animate-in slide-in-from-right duration-200 fixed inset-y-0 right-0 z-50 flex w-full flex-col bg-card shadow-xl sm:w-96"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border px-4 py-3">
          <div className="flex items-center gap-2">
            <div
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-lg text-sm font-bold text-white',
                channel.bgClass,
              )}
              aria-hidden="true"
            >
              {channel.initial}
            </div>
            <span className="font-semibold text-foreground">{channel.name}</span>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close drawer"
            className="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Step indicator */}
        <div className="border-b border-border px-4 py-3">
          <StepIndicator current={step} />
        </div>

        {/* Step content */}
        <div className="flex-1 overflow-y-auto p-4">
          {step === 1 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {t('drawer.steps.credentials')}
              </h2>
              <p className="text-sm text-muted-foreground">
                Enter your {channel.name} credentials
              </p>
              {channel.credentials.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No credentials required for this channel.
                </p>
              ) : (
                channel.credentials.map((cred) => (
                  <div key={cred.label} className="space-y-1.5">
                    <Label htmlFor={`cred-${cred.label}`}>{cred.label}</Label>
                    {cred.masked ? (
                      <MaskedInput
                        id={`cred-${cred.label}`}
                        placeholder={cred.placeholder}
                        value={fieldValues[cred.label] ?? ''}
                        onChange={(e) => handleFieldChange(cred.label, e.target.value)}
                      />
                    ) : (
                      <Input
                        id={`cred-${cred.label}`}
                        placeholder={cred.placeholder}
                        value={fieldValues[cred.label] ?? ''}
                        onChange={(e) => handleFieldChange(cred.label, e.target.value)}
                      />
                    )}
                  </div>
                ))
              )}
            </div>
          )}

          {step === 2 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {t('drawer.steps.verify')}
              </h2>
              <p className="text-sm text-muted-foreground">
                Test that the credentials you entered are valid.
              </p>
              <Button
                onClick={handleVerify}
                disabled={verifyState === 'loading'}
                className="w-full"
              >
                {verifyState === 'loading' ? t('drawer.verifying') : t('drawer.verifyCredentials')}
              </Button>
              {verifyState === 'success' && (
                <div className="flex items-center gap-2 rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600 dark:text-green-400">
                  <CheckCircle2 className="h-4 w-4 shrink-0" />
                  Credentials verified!
                </div>
              )}
              {verifyState === 'failure' && (
                <div className="flex items-center gap-2 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  <XCircle className="h-4 w-4 shrink-0" />
                  Invalid credentials — check and try again.
                </div>
              )}
            </div>
          )}

          {step === 3 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {t('drawer.steps.webhook')}
              </h2>
              <p className="text-sm text-muted-foreground">
                Add this webhook URL to your {channel.name} app settings.
              </p>
              <div className="rounded-md border border-border bg-muted p-3">
                <code className="break-all text-xs text-foreground">{webhookUrl}</code>
              </div>
              <Button
                variant="outline"
                onClick={handleCopy}
                className="w-full gap-2"
              >
                {copied ? (
                  <>
                    <Check className="h-4 w-4 text-green-500" />
                    {t('drawer.copied')}
                  </>
                ) : (
                  <>
                    <Copy className="h-4 w-4" />
                    {t('drawer.copyWebhook')}
                  </>
                )}
              </Button>
            </div>
          )}

          {step === 4 && (
            <div className="space-y-4">
              <h2 className="text-base font-semibold text-foreground">
                {t('drawer.steps.test')}
              </h2>
              <p className="text-sm text-muted-foreground">
                Send a test message to confirm the webhook is reachable.
              </p>
              <Button
                onClick={handleSendTest}
                disabled={testState === 'loading'}
                className="w-full"
              >
                {testState === 'loading' ? t('drawer.sending') : t('drawer.sendTest')}
              </Button>
              {(testState === 'success' || testState === 'done') && (
                <div className="flex items-center gap-2 rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600 dark:text-green-400">
                  <CheckCircle2 className="h-4 w-4 shrink-0" />
                  Test message received!
                </div>
              )}
              {testState === 'failure' && (
                <div className="flex items-center gap-2 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  <XCircle className="h-4 w-4 shrink-0" />
                  No message received. Check webhook.
                </div>
              )}
            </div>
          )}

          {step === 5 && (
            <div className="flex flex-col items-center gap-4 py-8 text-center">
              <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-500/15">
                <CheckCircle2 className="h-8 w-8 text-green-500" />
              </div>
              <p className="text-lg font-semibold text-foreground">{t('drawer.success')}</p>
              <p className="text-sm text-muted-foreground">
                {channel.name} is now active and receiving conversations.
              </p>
            </div>
          )}
        </div>

        {/* Footer navigation */}
        <div className="border-t border-border px-4 py-3">
          {step === 1 && (
            <Button onClick={() => setStep(2)} className="w-full">
              Next
            </Button>
          )}
          {step === 2 && (
            <Button
              onClick={() => setStep(3)}
              disabled={verifyState !== 'success'}
              className="w-full"
            >
              Next
            </Button>
          )}
          {step === 3 && (
            <Button onClick={() => setStep(4)} className="w-full">
              Next
            </Button>
          )}
          {step === 4 && (
            <Button
              onClick={() => setStep(5)}
              disabled={testState === 'idle' || testState === 'loading'}
              className="w-full"
            >
              Next
            </Button>
          )}
          {step === 5 && (
            <Button onClick={handleDone} className="w-full">
              {t('drawer.close')}
            </Button>
          )}
        </div>
      </div>
    </>
  );
}

// ---------------------------------------------------------------------------
// Web Widget config panel
// ---------------------------------------------------------------------------

type WidgetPosition = 'bottom-right' | 'bottom-left';

function WebWidgetConfig() {
  const t = useTranslations('chat.channels.widget');
  const [color, setColor] = React.useState('#6366f1');
  const [welcomeMsg, setWelcomeMsg] = React.useState('');
  const [position, setPosition] = React.useState<WidgetPosition>('bottom-right');

  return (
    <div className="mt-2 rounded-lg border border-border bg-muted/40 p-4">
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
              placeholder="Hi! How can I help you?"
              value={welcomeMsg}
              onChange={(e) => setWelcomeMsg(e.target.value)}
            />
          </div>

          <div className="space-y-1.5">
            <Label>{t('position')}</Label>
            <div className="flex gap-2">
              {(
                [
                  { value: 'bottom-right', label: t('positionBottomRight') },
                  { value: 'bottom-left', label: t('positionBottomLeft') },
                ] as { value: WidgetPosition; label: string }[]
              ).map(({ value, label }) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setPosition(value)}
                  className={cn(
                    'flex-1 rounded-md border px-3 py-2 text-xs font-medium transition-colors',
                    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                    position === value
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-border bg-background text-muted-foreground hover:border-ring/50 hover:text-foreground',
                  )}
                  aria-pressed={position === value}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Preview with mini browser chrome */}
        <div className="space-y-1.5">
          <Label>{t('preview')}</Label>
          <div className="overflow-hidden rounded-lg border border-border bg-background">
            {/* Traffic-light bar */}
            <div className="flex items-center gap-1.5 border-b border-border bg-muted/60 px-3 py-2">
              <span className="h-2.5 w-2.5 rounded-full bg-red-400" aria-hidden="true" />
              <span className="h-2.5 w-2.5 rounded-full bg-yellow-400" aria-hidden="true" />
              <span className="h-2.5 w-2.5 rounded-full bg-green-400" aria-hidden="true" />
            </div>
            {/* Viewport */}
            <div className="relative h-48">
              <div
                className={cn(
                  'absolute bottom-3 flex h-11 w-11 items-center justify-center rounded-full shadow-md transition-transform hover:scale-105',
                  position === 'bottom-right' ? 'right-3' : 'left-3',
                )}
                style={{ backgroundColor: color }}
                title="Chat bubble preview"
              >
                <MessageSquare className="h-5 w-5 text-white" />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}


// ---------------------------------------------------------------------------
// Channel row (list item)
// ---------------------------------------------------------------------------

interface ChannelRowProps {
  channel: Channel;
  onAction: (channel: Channel) => void;
  onDisconnect: (channelId: string) => void;
  isWidgetExpanded: boolean;
  onWidgetToggle: () => void;
}

function ChannelRow({
  channel,
  onAction,
  onDisconnect,
  isWidgetExpanded,
  onWidgetToggle,
}: ChannelRowProps) {
  const t = useTranslations('chat.channels');
  const isWeb = channel.id === 'web';

  function handleAction() {
    if (isWeb && channel.status === 'connected') {
      onWidgetToggle();
    } else {
      onAction(channel);
    }
  }

  return (
    <div>
      <div className="flex items-center gap-4 px-4 py-3.5">
        {/* Icon */}
        <div
          className={cn(
            'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-sm font-bold text-white',
            channel.bgClass,
          )}
          aria-hidden="true"
        >
          {channel.initial}
        </div>

        {/* Name + description */}
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-foreground">{channel.name}</p>
          <p className="text-xs text-muted-foreground">{channel.description}</p>
        </div>

        {/* Status badge */}
        <div className="hidden shrink-0 sm:block">
          {channel.status === 'connected' && (
            <Badge variant="outline" className="border-chatConfidence-high/30 text-chatConfidence-high">
              <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-chatConfidence-high" aria-hidden />
              {t('status.connected')}
              {channel.handle && (
                <span className="ml-1.5 text-muted-foreground font-normal">{channel.handle}</span>
              )}
            </Badge>
          )}
          {channel.status === 'error' && (
            <Badge variant="outline" className="border-destructive/30 text-destructive">
              <span className="mr-1.5 h-1.5 w-1.5 rounded-full bg-destructive" aria-hidden />
              {t('status.error')}
            </Badge>
          )}
          {channel.status === 'not_connected' && (
            <Badge variant="outline" className="text-muted-foreground">
              {t('status.notConnected')}
            </Badge>
          )}
        </div>

        {/* Actions */}
        <div className="flex shrink-0 items-center gap-2">
          {channel.status === 'not_connected' && (
            <Button onClick={handleAction} size="sm" className="h-8 px-4">
              {t('connect')}
            </Button>
          )}
          {channel.status === 'connected' && (
            <>
              <Button onClick={handleAction} size="sm" variant="outline" className="h-8 px-3">
                {t('configure')}
              </Button>
              <Button
                onClick={() => onDisconnect(channel.id)}
                size="sm"
                variant="ghost"
                className="h-8 px-3 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              >
                Disconnect
              </Button>
            </>
          )}
          {channel.status === 'error' && (
            <Button onClick={handleAction} size="sm" variant="destructive" className="h-8 px-3">
              Reconnect
            </Button>
          )}
        </div>
      </div>

      {/* Error message inline */}
      {channel.status === 'error' && channel.errorMsg && (
        <p className="px-4 pb-3 pl-[3.25rem] text-xs text-destructive">{channel.errorMsg}</p>
      )}

      {/* Web widget config (expandable) */}
      {isWeb && isWidgetExpanded && (
        <div className="border-t border-border px-4 pb-4 pt-4">
          <WebWidgetConfig />
        </div>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default function ChannelsPage() {
  const t = useTranslations('chat.channels');
  const [channels, setChannels] = React.useState<Channel[]>(INITIAL_CHANNELS);
  const [activeChannel, setActiveChannel] = React.useState<Channel | null>(null);
  const [widgetExpanded, setWidgetExpanded] = React.useState(false);

  function handleAction(channel: Channel) {
    setActiveChannel(channel);
  }

  function handleConnected(channelId: string) {
    setChannels((prev) =>
      prev.map((ch) => (ch.id === channelId ? { ...ch, status: 'connected' } : ch)),
    );
  }

  function handleDisconnect(channelId: string) {
    setChannels((prev) =>
      prev.map((ch) =>
        ch.id === channelId
          ? { ...ch, status: 'not_connected' as ChannelStatus, handle: undefined }
          : ch,
      ),
    );
  }

  function handleClose() {
    setActiveChannel(null);
  }

  return (
    <div className="flex-1 overflow-y-auto">
      <div className="mx-auto max-w-5xl space-y-6 p-4 sm:p-6">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground">{t('title')}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t('subtitle')}</p>
        </div>

        <div className="overflow-hidden rounded-lg border border-border bg-card">
          {channels.map((channel, idx) => (
            <div key={channel.id}>
              {idx > 0 && <div className="h-px bg-border" />}
              <ChannelRow
                channel={channel}
                onAction={handleAction}
                onDisconnect={handleDisconnect}
                isWidgetExpanded={widgetExpanded && channel.id === 'web'}
                onWidgetToggle={() => setWidgetExpanded((v) => !v)}
              />
            </div>
          ))}
        </div>

        {activeChannel && (
          <ConnectDrawer
            channel={activeChannel}
            onClose={handleClose}
            onConnected={handleConnected}
          />
        )}
      </div>
    </div>
  );
}