import { z } from 'zod';

/**
 * Connection health status shared by every settings slice that probes an external provider
 * (S3 storage, AI providers, …). The backend serializes `ConnectionStatus` (a C# enum) as its
 * STRING member name via `JsonStringEnumConverter` — never as a number — so this is modeled
 * as a string union, not `z.nativeEnum`/numeric schema.
 */
export const connectionHealthStatusSchema = z.enum(['Untested', 'Healthy', 'Degraded', 'Failed']);

export type ConnectionHealthStatus = z.infer<typeof connectionHealthStatusSchema>;

/**
 * Result of a `POST /api/v1/{feature}-config/test` endpoint. Always HTTP 200 — failure is
 * communicated via `success: false`, never a non-2xx status (S15).
 */
export const connectionHealthResultSchema = z.object({
  success: z.boolean(),
  status: connectionHealthStatusSchema,
  detail: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  latencyMs: z.number().nullable().optional(),
});

export type ConnectionHealthResult = z.infer<typeof connectionHealthResultSchema>;
