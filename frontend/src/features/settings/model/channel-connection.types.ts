import type { ConnectionHealthStatus } from './connection-health.schema';

export type ChannelType = 'whatsapp' | 'facebook' | 'instagram' | 'telegram' | 'web'
export type ConnectionStatus = 'connected' | 'disconnected' | 'error'

export interface ChannelConnectionDto {
  id: string
  channel: ChannelType
  displayName: string
  externalAccountId: string | null
  status: ConnectionStatus
  errorMessage: string | null
  isActive: boolean
  createdAt: string
  maskedAccessToken: string | null
  lastTestStatus: ConnectionHealthStatus
  lastTestedAt: string | null
  lastTestError: string | null
  lastTestLatencyMs: number | null
}

export interface SaveChannelConnectionRequest {
  channel: ChannelType
  displayName: string
  externalAccountId?: string
  accessToken: string
  appSecret?: string
  verifyToken?: string
}

/**
 * Result of `POST /api/v1/channel-connections/{id}/test` — the shared `ConnectionHealth` shape
 * (S3/AI slices reuse the same contract). Always HTTP 200; failure is `success: false` (S15).
 */
export type TestChannelConnectionResponse = {
  success: boolean
  status: ConnectionHealthStatus
  detail?: string | null
  errorMessage?: string | null
  latencyMs?: number | null
}
