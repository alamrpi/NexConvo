'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslations } from 'next-intl';
import { Eye, EyeOff, Loader2, UserPlus } from 'lucide-react';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { cn } from '@/shared/lib/cn';
import { signupSchema, type SignupValues } from '../model/signup.schema';
import { useSignup } from '../api/use-signup';

/**
 * Signup form — creates a tenant workspace + owner account. Client validation via RHF +
 * zod is UX only (S10); the Identity service authorizes/validates on the server via the
 * BFF. Fully labeled, keyboard-operable, with field and form-level error states (S11, S15, S27).
 */
export function SignupForm() {
  const t = useTranslations('signup');
  const router = useRouter();
  const signup = useSignup();
  const [showPassword, setShowPassword] = React.useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignupValues>({
    resolver: zodResolver(signupSchema),
    // Validate once a field is blurred, then live as the user fixes it (S15 — clear feedback).
    mode: 'onTouched',
    defaultValues: { tenantName: '', tenantSlug: '', fullName: '', email: '', password: '' },
  });

  const onSubmit = handleSubmit(async (values) => {
    const user = await signup.mutateAsync(values).catch(() => null);
    if (user) {
      window.location.assign('/dashboard');
    }
  });

  const errorText = (key?: string) => (key ? t(`errors.${key}`) : undefined);
  const busy = isSubmitting || signup.isPending;
  const formError = signup.isError ? t(`errors.${signup.error.code}`) : undefined;

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
        <Label htmlFor="tenantName">{t('tenantNameLabel')}</Label>
        <Input
          id="tenantName"
          autoComplete="organization"
          placeholder={t('tenantNamePlaceholder')}
          aria-invalid={!!errors.tenantName}
          aria-describedby={errors.tenantName ? 'tenantName-error' : undefined}
          {...register('tenantName')}
        />
        {errors.tenantName && (
          <p id="tenantName-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.tenantName.message)}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="tenantSlug">{t('tenantSlugLabel')}</Label>
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
            autoComplete="off"
            placeholder={t('tenantSlugPlaceholder')}
            aria-invalid={!!errors.tenantSlug}
            aria-describedby={errors.tenantSlug ? 'tenantSlug-error' : undefined}
            className="min-w-0 flex-1 bg-transparent px-3 text-sm outline-none placeholder:text-muted-foreground"
            {...register('tenantSlug')}
          />
          <span className="flex items-center border-l border-input bg-muted px-3 text-sm text-muted-foreground">
            {t('tenantSlugHint')}
          </span>
        </div>
        {errors.tenantSlug && (
          <p id="tenantSlug-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.tenantSlug.message)}
          </p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="fullName">{t('fullNameLabel')}</Label>
        <Input
          id="fullName"
          autoComplete="name"
          placeholder={t('fullNamePlaceholder')}
          aria-invalid={!!errors.fullName}
          aria-describedby={errors.fullName ? 'fullName-error' : undefined}
          {...register('fullName')}
        />
        {errors.fullName && (
          <p id="fullName-error" role="alert" className="text-sm text-destructive">
            {errorText(errors.fullName.message)}
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
        <Label htmlFor="password">{t('passwordLabel')}</Label>
        <div className="relative">
          <Input
            id="password"
            type={showPassword ? 'text' : 'password'}
            autoComplete="new-password"
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
            <UserPlus aria-hidden="true" />
            {t('submit')}
          </>
        )}
      </Button>
    </form>
  );
}
