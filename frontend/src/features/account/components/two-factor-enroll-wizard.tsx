'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { QRCodeSVG } from 'qrcode.react';
import { Check, Copy, Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/shared/ui/dialog';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Checkbox } from '@/shared/ui/checkbox';
import { Skeleton } from '@/shared/ui/skeleton';
import { totpCodeSchema, type TotpCodeValues } from '../model/two-factor.schema';
import {
  useConfirmTwoFactorEnrollment,
  useStartTwoFactorEnrollment,
} from '../api/use-two-factor';

type Step = 'scan' | 'codes';

/**
 * TOTP enrollment wizard (S15/S27). Step 1: render the otpauth:// URI as a scannable QR
 * (qrcode.react — client-side, no network) with the Base32 secret as a manual-entry
 * fallback, then verify a 6-digit code. Step 2: show the 10 backup codes once, gated
 * behind an explicit "I've saved these" acknowledgement before closing.
 */
export function TwoFactorEnrollWizard({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const t = useTranslations('settings.security.enroll');
  const start = useStartTwoFactorEnrollment();
  const confirm = useConfirmTwoFactorEnrollment();
  const [step, setStep] = React.useState<Step>('scan');
  const [backupCodes, setBackupCodes] = React.useState<string[]>([]);
  const [acknowledged, setAcknowledged] = React.useState(false);
  const startMutate = start.mutate;

  // Begin enrollment once when the dialog opens; reset everything when it closes.
  React.useEffect(() => {
    if (open) {
      setStep('scan');
      setBackupCodes([]);
      setAcknowledged(false);
      startMutate();
    }
    // startMutate is a stable mutate fn; we intentionally key only off `open`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // The backup codes are shown ONCE — don't let Esc / overlay / the X close that step until the
  // user has acknowledged saving them.
  const guardedOpenChange = (next: boolean) => {
    if (!next && step === 'codes' && !acknowledged) return;
    onOpenChange(next);
  };

  return (
    <Dialog open={open} onOpenChange={guardedOpenChange}>
      <DialogContent className="max-w-md" onEscapeKeyDown={(e) => step === 'codes' && !acknowledged && e.preventDefault()}>
        <DialogHeader>
          <DialogTitle>{t('title')}</DialogTitle>
          <DialogDescription>{step === 'scan' ? t('scanSubtitle') : t('codesSubtitle')}</DialogDescription>
        </DialogHeader>

        {step === 'scan' ? (
          <ScanStep
            enrollment={start.data}
            isLoading={start.isPending || (!start.data && !start.isError)}
            isError={start.isError}
            onRetry={() => startMutate()}
            confirmError={confirm.isError ? t(`errors.${confirm.error.code}`) : undefined}
            submitting={confirm.isPending}
            onSubmit={async (code) => {
              const result = await confirm.mutateAsync({ code }).catch(() => null);
              if (result) {
                setBackupCodes(result.backupCodes);
                setStep('codes');
              }
            }}
          />
        ) : (
          <CodesStep
            codes={backupCodes}
            acknowledged={acknowledged}
            onAcknowledge={setAcknowledged}
            onDone={() => onOpenChange(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function ScanStep({
  enrollment,
  isLoading,
  isError,
  onRetry,
  confirmError,
  submitting,
  onSubmit,
}: {
  enrollment: { secret: string; otpAuthUri: string } | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  confirmError?: string;
  submitting: boolean;
  onSubmit: (code: string) => Promise<void>;
}) {
  const t = useTranslations('settings.security.enroll');
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TotpCodeValues>({ resolver: zodResolver(totpCodeSchema), mode: 'onTouched', defaultValues: { code: '' } });

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="mx-auto h-44 w-44" />
        <Skeleton className="h-9 w-full" />
      </div>
    );
  }

  if (isError || !enrollment) {
    return (
      <div className="space-y-3">
        <p role="alert" className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {t('startError')}
        </p>
        <Button type="button" variant="outline" size="sm" onClick={onRetry} className="w-full">
          {t('retry')}
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(({ code }) => onSubmit(code))} noValidate className="space-y-4">
      {/* QR must stay black-on-white to remain scannable in both themes (intentional literal). */}
      <div className="mx-auto w-fit rounded-md bg-white p-3">
        <QRCodeSVG value={enrollment.otpAuthUri} size={168} aria-label={t('qrAlt')} />
      </div>

      <div className="space-y-1.5">
        <p className="text-xs text-muted-foreground">{t('secretHint')}</p>
        <CopyableSecret secret={enrollment.secret} />
      </div>

      <div className="space-y-1.5">
        <label htmlFor="totp-code" className="text-xs font-medium">
          {t('codeLabel')}
        </label>
        <Input
          id="totp-code"
          inputMode="numeric"
          autoComplete="one-time-code"
          placeholder="123456"
          maxLength={6}
          aria-invalid={!!errors.code}
          {...register('code')}
        />
        {errors.code && (
          <p role="alert" className="text-xs text-destructive">
            {t(`errors.${errors.code.message}`)}
          </p>
        )}
        {confirmError && !errors.code && (
          <p role="alert" className="text-xs text-destructive">
            {confirmError}
          </p>
        )}
      </div>

      <Button type="submit" className="w-full" disabled={submitting}>
        {submitting ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('verifying')}
          </>
        ) : (
          t('verify')
        )}
      </Button>
    </form>
  );
}

function CodesStep({
  codes,
  acknowledged,
  onAcknowledge,
  onDone,
}: {
  codes: string[];
  acknowledged: boolean;
  onAcknowledge: (value: boolean) => void;
  onDone: () => void;
}) {
  const t = useTranslations('settings.security.enroll');
  const headingRef = React.useRef<HTMLParagraphElement>(null);

  // Move focus to the codes step so screen-reader users hear the new content (S11).
  React.useEffect(() => headingRef.current?.focus(), []);

  const copyAll = () => void navigator.clipboard?.writeText(codes.join('\n')).catch(() => null);

  return (
    <div className="space-y-4">
      <p ref={headingRef} tabIndex={-1} className="text-sm font-medium outline-none">
        {t('codesHeading')}
      </p>
      <ul className="grid grid-cols-2 gap-2 rounded-md border border-border bg-muted/40 p-3 font-mono text-sm">
        {codes.map((code) => (
          <li key={code} className="text-center tracking-wide">
            {code}
          </li>
        ))}
      </ul>

      <Button type="button" variant="outline" size="sm" onClick={copyAll} className="w-full">
        <Copy aria-hidden="true" />
        {t('copyCodes')}
      </Button>

      <label className="flex items-start gap-2 text-sm">
        <Checkbox
          checked={acknowledged}
          onCheckedChange={(value) => onAcknowledge(value === true)}
          aria-label={t('acknowledge')}
          className="mt-0.5"
        />
        <span className="text-muted-foreground">{t('acknowledge')}</span>
      </label>

      <Button type="button" className="w-full" disabled={!acknowledged} onClick={onDone}>
        {t('done')}
      </Button>
    </div>
  );
}

function CopyableSecret({ secret }: { secret: string }) {
  const [copied, setCopied] = React.useState(false);
  const grouped = secret.replace(/(.{4})/g, '$1 ').trim();

  const copy = () => {
    void navigator.clipboard
      ?.writeText(secret)
      .then(() => {
        setCopied(true);
        window.setTimeout(() => setCopied(false), 1500);
      })
      .catch(() => null);
  };

  return (
    <div className="flex items-stretch gap-2">
      <code className="flex-1 select-all rounded-md border border-border bg-muted/40 px-3 py-2 font-mono text-xs tracking-wide">
        {grouped}
      </code>
      <Button type="button" variant="outline" size="icon" onClick={copy} aria-label="Copy secret">
        {copied ? <Check className="h-4 w-4 text-primary" /> : <Copy className="h-4 w-4" />}
      </Button>
    </div>
  );
}
