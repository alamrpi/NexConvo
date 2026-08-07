'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { CheckCircle2, Eye, EyeOff, Loader2, Save, XCircle, Zap } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Card, CardContent } from '@/shared/ui/card';
import { Skeleton } from '@/shared/ui/skeleton';
import { useSessionStore } from '@/features/auth/model/session.store';
import { s3ConfigSchema, type S3ConfigValues } from '../model/s3-config.schema';
import type { S3ConfigDto } from '../model/s3-config.types';
import { useS3Config } from '../api/use-s3-config';
import { S3ConfigError, useUpdateS3Config } from '../api/use-update-s3-config';
import { useTestS3Connection } from '../api/use-test-s3-connection';
import { HealthBadge } from './health-badge';

// ── Masked input (show/hide secret) ──────────────────────────────────────────

function SecretInput(props: React.ComponentProps<typeof Input> & { 'aria-label': string }) {
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
        {visible ? <EyeOff className="h-4 w-4" aria-hidden /> : <Eye className="h-4 w-4" aria-hidden />}
      </button>
    </div>
  );
}

// ── Field wrapper ─────────────────────────────────────────────────────────────

function Field({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id} className="text-sm">
        {label}
      </Label>
      {children}
      {hint && !error && <p className="text-xs text-muted-foreground">{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

// ── Health badge ──────────────────────────────────────────────────────────────

function S3HealthBadge({ config }: { config: S3ConfigDto }) {
  const t = useTranslations('settings.s3');
  return (
    <HealthBadge
      status={config.lastTestStatus}
      lastTestedAt={config.lastTestedAt}
      statusLabel={(status) => t(`health${status}` as 'healthHealthy')}
      lastTestedLabel={(relative) => (relative ? t('lastTested', { time: relative }) : t('lastTestedNever'))}
    />
  );
}

// ── Form skeleton ─────────────────────────────────────────────────────────────

function FormSkeleton() {
  return (
    <Card>
      <CardContent className="space-y-5 p-5">
        {Array.from({ length: 5 }).map((_, i) => (
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

// ── Main form ─────────────────────────────────────────────────────────────────

export function S3ConfigForm() {
  const t = useTranslations('settings.s3');
  const canManage = useSessionStore((s) => s.hasPermission('settings:manage'));
  const { data, isLoading, isError } = useS3Config();
  const update = useUpdateS3Config();
  const testConnection = useTestS3Connection();
  const [saved, setSaved] = React.useState(false);
  const [testState, setTestState] = React.useState<TestState>('idle');
  const [testResult, setTestResult] = React.useState<{ detail?: string | null; error?: string | null; latencyMs?: number | null }>({});

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isDirty },
  } = useForm<S3ConfigValues>({
    resolver: zodResolver(s3ConfigSchema),
    defaultValues: {
      bucketName: data?.bucketName ?? '',
      region: data?.region ?? '',
      customEndpoint: data?.customEndpoint ?? '',
      pathPrefix: data?.pathPrefix ?? '',
      isActive: data?.isActive ?? true,
    },
  });

  // A changed credential invalidates any prior test result — force a fresh test before saving.
  const watchedAccessKeyId = watch('accessKeyId');
  const watchedSecretAccessKey = watch('secretAccessKey');
  React.useEffect(() => {
    setTestState('idle');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [watchedAccessKeyId, watchedSecretAccessKey]);

  if (!canManage) {
    return (
      <p role="status" className="rounded-md border border-border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
        {t('noPermission')}
      </p>
    );
  }

  if (isLoading) return <FormSkeleton />;

  if (isError) {
    return (
      <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
        {t('loadError')}
      </p>
    );
  }

  const isConflict = update.error instanceof S3ConfigError && update.error.code === 'conflict';
  const isTestFailedOnSave = update.error instanceof S3ConfigError && update.error.code === 'test-failed';

  // A fresh, passing test is required whenever a credential is being entered/changed.
  const requiresTest = !!(watchedAccessKeyId || watchedSecretAccessKey);
  const canSave = !requiresTest || testState === 'success';

  const handleTest = handleSubmit(async (values) => {
    setTestState('testing');
    setTestResult({});
    const result = await testConnection.mutateAsync({
      bucketName: values.bucketName,
      region: values.region,
      accessKeyId: values.accessKeyId,
      secretAccessKey: values.secretAccessKey,
      customEndpoint: values.customEndpoint,
    });
    if (result.success) {
      setTestState('success');
      setTestResult({ detail: result.detail, latencyMs: result.latencyMs });
    } else {
      setTestState('failed');
      setTestResult({ error: result.errorMessage });
    }
  });

  async function onSubmit(values: S3ConfigValues) {
    setSaved(false);
    try {
      await update.mutateAsync(values);
      setSaved(true);
    } catch (error) {
      if (error instanceof S3ConfigError && error.code === 'test-failed') {
        setTestState('idle');
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <Card>
        <CardContent className="space-y-5 p-5">
          {data && <S3HealthBadge config={data} />}

          {update.isError && (
            <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {isConflict ? t('saveConflict') : isTestFailedOnSave ? t('credentialsExpired') : t('saveError')}
            </p>
          )}

          <div className="grid gap-5 sm:grid-cols-2">
            <Field
              id="bucketName"
              label={t('bucketNameLabel')}
              error={errors.bucketName?.message ? t(`errors.${errors.bucketName.message as 'bucketNameRequired'}`) : undefined}
            >
              <Input
                id="bucketName"
                placeholder={t('bucketNamePlaceholder')}
                aria-invalid={!!errors.bucketName}
                aria-describedby={errors.bucketName ? 'bucketName-error' : undefined}
                {...register('bucketName')}
              />
            </Field>

            <Field
              id="region"
              label={t('regionLabel')}
              hint={t('regionHint')}
              error={errors.region?.message ? t(`errors.${errors.region.message as 'regionRequired'}`) : undefined}
            >
              <Input
                id="region"
                placeholder={t('regionPlaceholder')}
                aria-invalid={!!errors.region}
                aria-describedby={errors.region ? 'region-error' : undefined}
                {...register('region')}
              />
            </Field>
          </div>

          <Field
            id="accessKeyId"
            label={t('accessKeyIdLabel')}
            hint={data?.hasAccessKey ? t('secretConfigured') : undefined}
            error={errors.accessKeyId?.message}
          >
            <SecretInput
              id="accessKeyId"
              aria-label={t('accessKeyIdLabel')}
              placeholder={data?.hasAccessKey ? t('secretPlaceholder') : t('accessKeyIdPlaceholder')}
              aria-invalid={!!errors.accessKeyId}
              aria-describedby={errors.accessKeyId ? 'accessKeyId-error' : undefined}
              {...register('accessKeyId')}
            />
          </Field>

          <Field
            id="secretAccessKey"
            label={t('secretAccessKeyLabel')}
            hint={data?.hasAccessKey ? t('secretConfigured') : undefined}
            error={errors.secretAccessKey?.message}
          >
            <SecretInput
              id="secretAccessKey"
              aria-label={t('secretAccessKeyLabel')}
              placeholder={data?.hasAccessKey ? t('secretPlaceholder') : t('secretAccessKeyPlaceholder')}
              aria-invalid={!!errors.secretAccessKey}
              aria-describedby={errors.secretAccessKey ? 'secretAccessKey-error' : undefined}
              {...register('secretAccessKey')}
            />
          </Field>

          <Field
            id="customEndpoint"
            label={t('customEndpointLabel')}
            hint={t('customEndpointHint')}
            error={errors.customEndpoint?.message ? t(`errors.${errors.customEndpoint.message as 'customEndpointInvalid'}`) : undefined}
          >
            <Input
              id="customEndpoint"
              placeholder={t('customEndpointPlaceholder')}
              aria-invalid={!!errors.customEndpoint}
              aria-describedby={errors.customEndpoint ? 'customEndpoint-error' : undefined}
              {...register('customEndpoint')}
            />
          </Field>

          <Field
            id="pathPrefix"
            label={t('pathPrefixLabel')}
            hint={t('pathPrefixHint')}
            error={errors.pathPrefix?.message ? t(`errors.${errors.pathPrefix.message as 'pathPrefixInvalid'}`) : undefined}
          >
            <Input
              id="pathPrefix"
              placeholder={t('pathPrefixPlaceholder')}
              aria-invalid={!!errors.pathPrefix}
              aria-describedby={errors.pathPrefix ? 'pathPrefix-error' : undefined}
              {...register('pathPrefix')}
            />
          </Field>

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
              {testResult.error ?? t('testFailed')}
            </p>
          )}
          {requiresTest && testState === 'idle' && (
            <p role="status" className="text-xs text-muted-foreground">
              {t('testRequired')}
            </p>
          )}
        </CardContent>
      </Card>

      <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center">
        <Button
          type="button"
          variant="outline"
          disabled={testState === 'testing' || update.isPending}
          onClick={handleTest}
        >
          {testState === 'testing' ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />
              {t('testingConnection')}
            </>
          ) : (
            <>
              <Zap className="mr-2 h-4 w-4" aria-hidden />
              {t('testConnection')}
            </>
          )}
        </Button>

        <Button type="submit" disabled={update.isPending || !isDirty || !canSave}>
          {update.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden />
              {t('saving')}
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" aria-hidden />
              {t('save')}
            </>
          )}
        </Button>

        {saved && !update.isPending && (
          <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
            <CheckCircle2 className="h-4 w-4 text-chatConfidence-high" aria-hidden />
            {t('saved')}
          </span>
        )}
      </div>
    </form>
  );
}
