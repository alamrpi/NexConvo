/**
 * Communication channels surfaced on the dashboard. The dot color maps to a
 * semantic chart/status token (S25) — no raw hex — so both themes stay in parity
 * while each channel reads as visually distinct.
 */
export type Channel = 'WhatsApp' | 'Email' | 'Instagram' | 'Voice' | 'Telegram';

export const channelDot: Record<Channel, string> = {
  WhatsApp: 'bg-chart-4',
  Email: 'bg-chart-1',
  Instagram: 'bg-chart-5',
  Voice: 'bg-warning',
  Telegram: 'bg-chart-3',
};
