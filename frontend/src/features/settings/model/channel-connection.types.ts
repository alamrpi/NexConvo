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
}

export interface SaveChannelConnectionRequest {
  channel: ChannelType
  displayName: string
  externalAccountId?: string
  accessToken: string
  appSecret?: string
  verifyToken?: string
}

export interface TestChannelConnectionResponse {
  success: boolean
  accountName?: string
  errorMessage?: string
}
