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
