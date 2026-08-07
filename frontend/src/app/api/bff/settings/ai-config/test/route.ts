import { NextResponse } from 'next/server';
import { z } from 'zod';
import { withBff } from '@/shared/api/server/bff';
import { supportedAiProviderSchema } from '@/features/settings/model/ai-settings.schema';

const testSchema = z.object({
  provider: supportedAiProviderSchema,
  apiKey: z.string().optional(),
  baseUrl: z.string().optional(),
  model: z.string().min(1),
});

/**
 * Tests AI provider connectivity without saving. Always forwards the gateway's HTTP 200 body —
 * `ConnectionHealth` communicates failure via `success: false`, never via a non-2xx status, so
 * there is no try/catch here mapping error statuses (S15). Mirrors the S3 test BFF route.
 */
export const POST = withBff(async (req, { api }) => {
  const parsed = testSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ success: false, status: 'Failed', errorMessage: 'Invalid input' }, { status: 422 });
  }

  const { data } = await api.post('/api/v1/ai-config/test', parsed.data);
  return NextResponse.json(data);
});
