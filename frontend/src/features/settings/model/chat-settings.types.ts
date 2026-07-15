import type { AiProviderType } from './ai-settings.types';

export type SentimentSensitivity = 'low' | 'medium' | 'high';
export type PiiMaskingLevel = 'off' | 'standard' | 'strict';

export interface WorkspaceChatSettingsDto {
  primaryProvider: AiProviderType;
  primaryModel: string;
  fallbackProviders: AiProviderType[];
  systemPromptOverride: string | null;
  handoffConfidenceThreshold: number;
  sentimentEscalationEnabled: boolean;
  sentimentSensitivity: SentimentSensitivity;
  triggerPhrases: string[];
  maxUnansweredMessages: number;
  piiMaskingLevel: PiiMaskingLevel;
  dataRetentionDays: number | null;
  widgetIconUrl: string | null;
  widgetPrimaryColor: string;
  widgetSecondaryColor: string;
  widgetWelcomeMessage: string;
}
