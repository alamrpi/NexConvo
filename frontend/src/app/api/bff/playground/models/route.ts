import { NextResponse } from 'next/server';
import { withBff } from '@/shared/api/server/bff';

export const GET = withBff(
  async (req, { api }) => {
    const res = await api.get('/api/v1/playground/models');
    return NextResponse.json(res.data);
  },
  () => {
    return NextResponse.json([
      {
        providerId: 'openrouter',
        providerName: 'OpenRouter',
        models: [
          { modelId: 'anthropic/claude-3-haiku', modelName: 'Claude 3 Haiku' },
          { modelId: 'gpt-4o-mini', modelName: 'GPT-4o Mini' },
        ],
      },
    ]);
  }
);
