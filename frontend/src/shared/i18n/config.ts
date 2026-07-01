/** Supported locales. Bengali is a first-class locale (frontend standard S12). */
export const locales = ['en', 'bn'] as const;

export type Locale = (typeof locales)[number];

export const defaultLocale: Locale = 'en';

/** Single source of truth for the locale cookie name (S8 pattern). */
export const LOCALE_COOKIE = 'nexconvo_locale';

export function isLocale(value: string | undefined | null): value is Locale {
  return value != null && (locales as readonly string[]).includes(value);
}
