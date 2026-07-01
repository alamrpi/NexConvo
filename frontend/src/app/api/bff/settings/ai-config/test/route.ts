import { NextResponse } from 'next/server';
import axios from 'axios';
import { z } from 'zod';
import { withBff } from '@/shared/api/server/bff';
import { supportedAiProviderSchema } from '@/features/settings/model/ai-settings.schema';

const testSchema = z.object({
  provider: supportedAiProviderSchema,
  apiKey: z.string().optional(),
  baseUrl: z.string().optional(),
  model: z.string().min(1),
});

export const POST = withBff(async (req, { api }) => {
  const parsed = testSchema.safeParse(await req.json().catch(() => null));
  if (!parsed.success) {
    return NextResponse.json({ success: false, error: 'Invalid input' }, { status: 422 });
  }

  try {
    const { data } = await api.post<{ success: boolean; error?: string }>(
      '/api/v1/ai-config/test',
      parsed.data,
    );
    return NextResponse.json(data);
  } catch (error) {
    if (axios.isAxiosError(error)) {
      const message = error.response?.data?.error ?? 'Connection test failed.';
      return NextResponse.json({ success: false, error: message });
    }
    throw error;
  }
});
