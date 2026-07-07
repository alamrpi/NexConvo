'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { CheckCircle2, Loader2, RefreshCw, Save, XCircle, Zap } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Textarea } from '@/shared/ui/textarea';
import { Label } from '@/shared/ui/label';
import { Card, CardContent } from '@/shared/ui/card';
import { Skeleton } from '@/shared/ui/skeleton';
import { cn } from '@/shared/lib/cn';
import { useSessionStore } from '@/features/auth/model/session.store';
import {
  aiSettingsSchema,
  supportedAiProviderSchema,
  type AiSettingsValues,
} from '../model/ai-settings.schema';
import type { AiConfigDto, AiProviderType } from '../model/ai-settings.types';
import type { ConnectionHealthResult } from '../model/connection-health.schema';
import { useAiSettings } from '../api/use-ai-settings';
import { AiSettingsError, useUpdateAiSettings } from '../api/use-update-ai-settings';
import { useTestAiConnection } from '../api/use-test-ai-connection';
import { HealthBadge } from './health-badge';

const MODEL_HINTS: Record<AiProviderType, string> = {
  OpenAI: 'gpt-4o',
  Anthropic: 'claude-sonnet-4-6',
  Gemini: 'gemini-2.0-flash',
  OpenRouter: 'openai/gpt-4o',
  DeepSeek: 'deepseek-chat',
};

const DEFAULT_MODELS: Record<AiProviderType, string> = {
  OpenAI: 'gpt-4o',
  Anthropic: 'claude-sonnet-4-6',
  Gemini: 'gemini-2.0-flash',
  OpenRouter: 'openai/gpt-4o',
  DeepSeek: 'deepseek-chat',
};

export function AiSettingsForm() {
  const t = useTranslations('settings.ai');
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useAiSettings();
  const [selectedProvider, setSelectedProvider] = React.useState<AiProviderType>('OpenAI');

  if (!canManage) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  if (isLoading) return <FormSkeleton />;

  if (isError || !data) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  const configForProvider = data.find((c) => c.provider === selectedProvider) ?? null;

  return (
    <div className="space-y-4">
      {/* Provider tabs */}
      <div
        className="flex gap-1 rounded-lg bg-muted p-1"
        role="tablist"
        aria-label={t('providerLabel')}
      >
        {supportedAiProviderSchema.options.map((p) => {
          const saved = data.find((c) => c.provider === p);
          return (
            <button
              key={p}
              role="tab"
              type="button"
              aria-selected={selectedProvider === p}
              onClick={() => setSelectedProvider(p)}
              className={cn(
                'relative flex flex-1 items-center justify-center gap-1.5 rounded-md px-2 py-1.5 text-xs font-medium transition-colors sm:text-sm',
                selectedProvider === p
                  ? 'bg-background text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {p}
              {saved?.isActive && (
                <span className="ml-0.5 h-1.5 w-1.5 rounded-full bg-primary" aria-label="active" />
              )}
            </button>
          );
        })}
      </div>

      {/* Form panel for selected provider */}
      <AiSettingsFields
        key={selectedProvider}
        provider={selectedProvider}
        config={configForProvider}
      />
    </div>
  );
}

function AiHealthBadge({ config }: { config: AiConfigDto }) {
  const t = useTranslations('settings.ai');
  return (
    <HealthBadge
      status={config.lastTestStatus}
      lastTestedAt={config.lastTestedAt}
      statusLabel={(status) => t(`health${status}` as 'healthHealthy')}
      lastTestedLabel={(relative) => (relative ? t('lastTested', { time: relative }) : t('lastTestedNever'))}
    />
  );
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
      </CardContent>
    </Card>
  );
}

type TestState = 'idle' | 'testing' | 'success' | 'failed';

function AiSettingsFields({
  provider,
  config,
}: {
  provider: AiProviderType;
  config: AiConfigDto | null;
}) {
  const t = useTranslations('settings.ai');
  const update = useUpdateAiSettings();
  const testConnection = useTestAiConnection();
  const [replacingSecret, setReplacingSecret] = React.useState(!config?.hasApiKey);
  const [testState, setTestState] = React.useState<TestState>('idle');
  const [testResult, setTestResult] = React.useState<
    Pick<ConnectionHealthResult, 'detail' | 'errorMessage' | 'latencyMs'>
  >({});

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<AiSettingsValues>({
    resolver: zodResolver(aiSettingsSchema),
    mode: 'onTouched',
    defaultValues: {
      provider,
      apiKey: '',
      baseUrl: config?.baseUrl ?? '',
      defaultModel: config?.defaultModel ?? DEFAULT_MODELS[provider],
      parameters: config?.parameters ?? '',
      isActive: config?.isActive ?? true,
    },
  });

  // Reset test state whenever key/model/baseUrl changes — a changed input invalidates any prior test.
  const watchedKey = watch('apiKey');
  const watchedModel = watch('defaultModel');
  const watchedBaseUrl = watch('baseUrl');
  React.useEffect(() => {
    if (testState !== 'idle') setTestState('idle');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [watchedKey, watchedModel, watchedBaseUrl]);

  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || update.isPending;
  const isConflict = update.error instanceof AiSettingsError && update.error.code === 'conflict';
  const isTestFailedOnSave = update.error instanceof AiSettingsError && update.error.code === 'test-failed';

  // Save requires a passing test when key is being set for the first time or replaced.
  const requiresTest = !config?.hasApiKey || replacingSecret;
  const canSave = !requiresTest || testState === 'success';

  const handleTest = handleSubmit(async (values) => {
    setTestState('testing');
    setTestResult({});
    const result = await testConnection.mutateAsync({
      provider,
      apiKey: values.apiKey || undefined,
      baseUrl: values.baseUrl || undefined,
      model: values.defaultModel,
    });
    if (result.success) {
      setTestState('success');
      setTestResult({ detail: result.detail, latencyMs: result.latencyMs });
    } else {
      setTestState('failed');
      setTestResult({ errorMessage: result.errorMessage });
    }
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      await update.mutateAsync(values);
    } catch (error) {
      // Server re-tested the credentials on save and they no longer work — force a fresh test.
      if (error instanceof AiSettingsError && error.code === 'test-failed') {
        setTestState('idle');
      }
    }
  });

  return (
    <Card>
      <CardContent className="p-5">
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          {update.isSuccess && (
            <p role="status" className="rounded-md border border-primary/30 bg-primary/5 px-3 py-2 text-sm text-primary">
              {t('saved')}
            </p>
          )}
          {update.isError && (
            <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {isConflict ? t('saveConflict') : isTestFailedOnSave ? t('credentialsExpired') : t('saveError')}
            </p>
          )}

          {config && <AiHealthBadge config={config} />}

          {/* Hidden provider field */}
          <input type="hidden" {...register('provider')} value={provider} />

          <div className="grid gap-4 sm:grid-cols-2">
            <Field id="defaultModel" label={t('defaultModelLabel')} error={errorText(errors.defaultModel?.message)}>
              <Input
                id="defaultModel"
                placeholder={MODEL_HINTS[provider]}
                {...register('defaultModel')}
                aria-invalid={!!errors.defaultModel}
                aria-describedby={errors.defaultModel ? 'defaultModel-error' : undefined}
              />
            </Field>
            <Field id="baseUrl" label={t('baseUrlLabel')} error={errorText(errors.baseUrl?.message)}>
              <Input
                id="baseUrl"
                {...register('baseUrl')}
                aria-invalid={!!errors.baseUrl}
                aria-describedby={errors.baseUrl ? 'baseUrl-error' : undefined}
                placeholder="https://..."
              />
            </Field>
          </div>

          <Field id="apiKey" label={t('apiKeyLabel')} error={errorText(errors.apiKey?.message)}>
            {config?.hasApiKey && !replacingSecret ? (
              <div className="flex gap-2">
                <Input id="apiKey" value="••••••••" disabled readOnly aria-label={t('secretConfigured')} />
                <Button type="button" variant="outline" size="sm" onClick={() => setReplacingSecret(true)}>
                  <RefreshCw aria-hidden="true" className="mr-2 h-4 w-4" />
                  {t('replace')}
                </Button>
              </div>
            ) : (
              <Input
                id="apiKey"
                type="password"
                autoComplete="new-password"
                placeholder={t('secretPlaceholder')}
                aria-invalid={!!errors.apiKey}
                aria-describedby={errors.apiKey ? 'apiKey-error' : undefined}
                {...register('apiKey')}
              />
            )}
          </Field>

          <Field id="parameters" label={t('parametersLabel')} error={errorText(errors.parameters?.message)}>
            <Textarea
              id="parameters"
              rows={3}
              className="font-mono"
              placeholder='{ "temperature": 0.7 }'
              {...register('parameters')}
              aria-invalid={!!errors.parameters}
              aria-describedby={errors.parameters ? 'parameters-error' : undefined}
            />
          </Field>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              className="h-4 w-4 accent-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-1"
              {...register('isActive')}
            />
            {t('activeLabel')}
          </label>

          {/* Connection test feedback */}
          {testState === 'success' && (
            <p role="status" className="flex items-center gap-2 text-sm text-primary">
              <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
              {testResult.detail ?? t('testPassed')}
              {typeof testResult.latencyMs === 'number' && (
                <span className="text-muted-foreground">({testResult.latencyMs} ms)</span>
              )}
            </p>
          )}
          {testState === 'failed' && (
            <p role="alert" className="flex items-center gap-2 text-sm text-destructive">
              <XCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
              {testResult.errorMessage ?? t('testFailed')}
            </p>
          )}
          {requiresTest && testState === 'idle' && (
            <p role="status" className="text-xs text-muted-foreground">
              {t('testRequired')}
            </p>
          )}

          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-end">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={testState === 'testing' || busy}
              onClick={handleTest}
            >
              {testState === 'testing' ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {t('testingConnection')}
                </>
              ) : (
                <>
                  <Zap aria-hidden="true" className="mr-2 h-4 w-4" />
                  {t('testConnection')}
                </>
              )}
            </Button>

            <Button type="submit" size="sm" disabled={busy || !canSave}>
              {busy ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  {t('saving')}
                </>
              ) : (
                <>
                  <Save aria-hidden="true" className="mr-2 h-4 w-4" />
                  {t('save')}
                </>
              )}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
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
      <Label htmlFor={id} className="text-xs">
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
