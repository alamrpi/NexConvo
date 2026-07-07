import { z } from 'zod';

const S3_BUCKET_RE = /^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$/;

function isValidEndpointUrl(v: string) {
  try {
    const url = new URL(v);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

export const s3ConfigSchema = z.object({
  bucketName: z
    .string()
    .min(3, 'bucketNameRequired')
    .max(63, 'bucketNameTooLong')
    .regex(S3_BUCKET_RE, 'bucketNameInvalid'),
  region: z.string().min(1, 'regionRequired').max(50),
  accessKeyId: z.string().optional(),
  secretAccessKey: z.string().optional(),
  customEndpoint: z
    .string()
    .max(500)
    .refine((v) => !v || isValidEndpointUrl(v), { message: 'customEndpointInvalid' })
    .optional(),
  pathPrefix: z
    .string()
    .max(200)
    .refine((v) => !v || (!v.startsWith('/') && v.endsWith('/')), {
      message: 'pathPrefixInvalid',
    })
    .optional(),
  isActive: z.boolean().default(true),
});

export type S3ConfigValues = z.infer<typeof s3ConfigSchema>;

/**
 * Connection health status. The backend serializes `ConnectionStatus` (a C# enum) as its
 * STRING member name via `JsonStringEnumConverter` — never as a number — so this is modeled
 * as a string union, not `z.nativeEnum`/numeric schema.
 */
export const s3HealthStatusSchema = z.enum(['Untested', 'Healthy', 'Degraded', 'Failed']);

export type S3HealthStatus = z.infer<typeof s3HealthStatusSchema>;

/** Result of `POST /api/v1/s3-config/test`. Always HTTP 200 — failure is `success: false`. */
export const s3TestResultSchema = z.object({
  success: z.boolean(),
  status: s3HealthStatusSchema,
  detail: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  latencyMs: z.number().nullable().optional(),
});

export type S3TestResult = z.infer<typeof s3TestResultSchema>;
