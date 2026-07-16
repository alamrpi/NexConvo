import { z } from 'zod';

/**
 * Environment configuration, validated once at module load (frontend standard S13).
 *
 * - `publicEnv` holds only NEXT_PUBLIC_* values that are safe in the browser bundle.
 * - `serverEnv()` validates server-only secrets (gateway URL, keys) and must NEVER be
 *   imported into a client component — calling it on the client throws by design.
 */

const publicSchema = z.object({
  NEXT_PUBLIC_APP_NAME: z.string().min(1).default('NexConvo'),
  NEXT_PUBLIC_APP_URL: z.string().url().default('http://localhost:3000'),
  // Public origin that serves the embeddable widget bundle (non-secret). Used to build the
  // copy-paste embed snippet shown in Settings — never hardcode a localhost URL there.
  NEXT_PUBLIC_WIDGET_URL: z.string().url().default('http://localhost:5173'),
});

// Referencing the keys explicitly keeps them inlined by Next at build time.
export const publicEnv = publicSchema.parse({
  NEXT_PUBLIC_APP_NAME: process.env.NEXT_PUBLIC_APP_NAME,
  NEXT_PUBLIC_APP_URL: process.env.NEXT_PUBLIC_APP_URL,
  NEXT_PUBLIC_WIDGET_URL: process.env.NEXT_PUBLIC_WIDGET_URL,
});

const serverSchema = z.object({
  API_GATEWAY_URL: z.string().url(),
});

export type ServerEnv = z.infer<typeof serverSchema>;

export function serverEnv(): ServerEnv {
  if (typeof window !== 'undefined') {
    throw new Error('serverEnv() was called on the client — server-only config must never reach the browser.');
  }
  return serverSchema.parse({
    API_GATEWAY_URL: process.env.API_GATEWAY_URL,
  });
}
