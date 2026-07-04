export type SourceType = 'file' | 'url' | 'text' | 'faq_pairs' | 'past_chats';
export type IngestionStatus = 'pending' | 'processing' | 'active' | 'failed' | 'inactive';

export interface KnowledgeDocumentDto {
  id: string;
  title: string;
  sourceType: SourceType;
  status: IngestionStatus;
  chunkCount: number;
  embeddingModel: string;
  failureReason: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgeChunkDto {
  id: string;
  ordinal: number;
  contentPreview: string;
  tokenCount: number;
}

export interface KnowledgeDocumentVersionDto {
  version: number;
  embeddingModel: string;
  chunkCount: number;
  createdAt: string;
}

export interface KnowledgeDocumentDetailDto extends KnowledgeDocumentDto {
  chunks: KnowledgeChunkDto[];
  versionHistory: KnowledgeDocumentVersionDto[];
}

export interface KnowledgeDocumentPagedResult {
  items: KnowledgeDocumentDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UploadKnowledgeDocumentInput {
  file?: File;
  title: string;
  sourceType: SourceType;
  content?: string;
  sourceUrl?: string;
  /** Q&A pairs serialised as JSON when sourceType is faq_pairs */
  faqPairs?: string;
  dateFrom?: string;
  dateTo?: string;
}
