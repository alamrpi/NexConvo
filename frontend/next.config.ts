import type { NextConfig } from 'next';
import createNextIntlPlugin from 'next-intl/plugin';

// Point next-intl at our request config (cookie-based locale, no URL prefix).
const withNextIntl = createNextIntlPlugin('./src/shared/i18n/request.ts');

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  // Pin the workspace root to this app dir so a stray lockfile elsewhere in the repo can't
  // confuse Next's root inference (the cause of the multi-lockfile warning).
  outputFileTracingRoot: __dirname,
};

export default withNextIntl(nextConfig);
