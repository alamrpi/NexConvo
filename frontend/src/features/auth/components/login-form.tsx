'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Eye, EyeOff, Loader2, LogIn } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { cn } from '@/shared/lib/cn';
import { loginSchema, type LoginValues } from '../model/login.schema';
import { useLogin } from '../api/use-login';
import { LoginTwoFactorStep } from './login-two-factor-step';

/**
 * Login form. Client validation via RHF + zod is UX only (S10) — the Identity service
 * authorizes on the server via the BFF. Fully labeled, keyboard-operable, with visible
 * field and form-level error/focus states (S11, S15, S27). When the account has 2FA, login
 * pauses on a second step for the authenticator code.
 */
export function LoginForm() {
  const t = useTranslations('login');
  const router = useRouter();
  const login = useLogin();
  const [showPassword, setShowPassword] = React.useState(false);
  const [twoFactor, setTwoFactor] = React.useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    // Validate once a field is blurred, then live as the user fixes it (S15 — clear feedback).
    mode: 'onTouched',
    defaultValues: { tenantSlug: '', email: '', password: '' },
  });

  const goToDashboard = () => {
    // replace() renders the dashboard's server layout fresh (re-reading the new session
    // cookies); a router.refresh() here races and can abort the navigation.
    router.replace('/dashboard');
  };

  const onSubmit = handleSubmit(async (values) => {
    const outcome = await login.mutateAsync(values).catch(() => null);
    if (!outcome) return;
    if (outcome.kind === 'twoFactorRequired') {
      setTwoFactor(true);
    } else {
      goToDashboard();
    }
  });

  if (twoFactor) {
    return <LoginTwoFactorStep onVerified={goToDashboard} onRestart={() => setTwoFactor(false)} />;
  }

  /** Resolve a field's error-key into a localized message. */
  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);

  const busy = isSubmitting || login.isPending;
  const formError = login.isError ? t(`errors.${login.error.code}`) : undefined;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-5">
      {formError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {formError}
        </div>
      )}
      <div className="space-y-2">
        <Label htmlFor="tenantSlug">{t('tenantLabel')}</Label>
        <div
          className={cn(
            'flex h-9 items-stretch overflow-hidden rounded-md border border-input bg-background shadow-sm transition-[border-color,box-shadow]',
            'hover:border-ring/60',
            'focus-within:border-ring focus-within:ring-[3px] focus-within:ring-ring/20',
            'has-[[aria-invalid=true]]:border-destructive has-[[aria-invalid=true]]:ring-[3px] has-[[aria-invalid=true]]:ring-destructive/20',
          )}
        >
          <input
            id="tenantSlug"
            autoComplete="organization"
            placeholder={t('tenantPlaceholder')}
            aria-invalid={!!errors.tenantSlug}
            aria-describedby={errors.tenantSlug ? 'tenantSlug-error' : undefined}
            className="min-w-0 flex-1 bg-transparent px-3 text-sm outline-none placeholder:text-muted-foreground"
            {...register('tenantSlug')}
          />
          <span className="flex items-center border-l border-input bg-muted px-3 text-sm text-muted-foreground">
            {t('tenantHint')}
          </span>
        </div>
        {errors.tenantSlug && (
          <p id="tenantSlug-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.tenantSlug.message)}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="email">{t('emailLabel')}</Label>
        <Input
          id="email"
          type="email"
          autoComplete="email"
          placeholder={t('emailPlaceholder')}
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? 'email-error' : undefined}
          {...register('email')}
        />
        {errors.email && (
          <p id="email-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.email.message)}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label htmlFor="password">{t('passwordLabel')}</Label>
          <Link
            href="/forgot-password"
            className="text-sm font-medium text-primary underline-offset-4 hover:underline"
          >
            {t('forgotPassword')}
          </Link>
        </div>
        <div className="relative">
          <Input
            id="password"
            type={showPassword ? 'text' : 'password'}
            autoComplete="current-password"
            placeholder={t('passwordPlaceholder')}
            aria-invalid={!!errors.password}
            aria-describedby={errors.password ? 'password-error' : undefined}
            className="pr-10"
            {...register('password')}
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            aria-label={showPassword ? t('hidePassword') : t('showPassword')}
            aria-pressed={showPassword}
            className="absolute inset-y-0 right-0 flex w-10 items-center justify-center rounded-r-md text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
          </button>
        </div>
        {errors.password && (
          <p id="password-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.password.message)}
          </p>
        )}
      </div>

      <Button type="submit" size="lg" className="w-full" disabled={busy}>
        {busy ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            {t('submitting')}
          </>
        ) : (
          <>
            <LogIn aria-hidden="true" />
            {t('submit')}
          </>
        )}
      </Button>
    </form>
  );
}
