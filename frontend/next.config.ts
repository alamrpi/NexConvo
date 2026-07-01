import type { NextConfig } from 'next';
import createNextIntlPlugin from 'next-intl/plugin';

// Point next-intl at our request config (cookie-based locale, no URL prefix).
const withNextIntl = createNextIntlPlugin('./src/shared/i18n/request.ts');

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
};

export default withNextIntl(nextConfig);
