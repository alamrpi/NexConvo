import type { z } from 'zod';
import type { aiProviderTypeSchema } from './ai-settings.schema';

export type AiProviderType = z.infer<typeof aiProviderTypeSchema>;

export interface AiConfigDto {
  id: string;
  provider: AiProviderType;
  hasApiKey: boolean;
  baseUrl: string | null;
  defaultModel: string;
  parameters: string | null;
  isActive: boolean;
}
