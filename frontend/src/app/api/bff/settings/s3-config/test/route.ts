import { NextResponse } from 'next/server';
import { z } from 'zod';
import { withBff } from '@/shared/api/server/bff';

const testSchema = z.object({
  bucketName: z.string().min(1),
  region: z.string().min(1),
  accessKeyId: z.string().optional(),
  secretAccessKey: z.string().optional(),
  customEndpoint: z.string().optional(),
});

/**
 * Tests S3 connectivity without saving. Always forwards the gateway's HTTP 200 body —
 * `ConnectionHealth` communicates failure via `success: false`, never via a non-2xx status,
 * so there is no try/catch here mapping error statuses (S15).
 */
export const POST = withBff(async (req, { api }) => {
  const parsed = testSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ success: false, status: 'Failed', errorMessage: 'Invalid input' }, { status: 422 });
  }

  const { data } = await api.post('/api/v1/s3-config/test', parsed.data);
  return NextResponse.json(data);
});
