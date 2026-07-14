/**
 * Mirrors the backend's SourceType enum exactly (File/Url/Text/Faq, PascalCase — same
 * JsonStringEnumConverter as DocumentStatus). There is no "past_chats" source on the wire;
 * ASP.NET's default form-model binder accepts either casing on upload, so this union is only
 * strict about what read endpoints actually return.
 */
export type SourceType = 'File' | 'Url' | 'Text' | 'Faq';

/**
 * Mirrors the backend's DocumentStatus enum exactly (Pending/Processing/Ready/Failed,
 * PascalCase — Knowledge.Api serializes enums via JsonStringEnumConverter with no naming
 * policy override). There is no "inactive" status on the wire; a soft-deleted document is
 * simply excluded from the list query, never returned with a distinct status value.
 */
export type IngestionStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed';

/**
 * Shape returned by the LIST endpoint (GetKnowledgeDocumentsPagedQuery →
 * KnowledgeDocumentSummaryDto). Deliberately does NOT include embeddingModel — the list query
 * never selects it; only the detail endpoint does (see KnowledgeDocumentDetailDto).
 */
export interface KnowledgeDocumentDto {
  id: string;
  title: string;
  fileName: string;
  sourceType: SourceType;
  status: IngestionStatus;
  chunkCount: number;
  failureReason: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface KnowledgeChunkDto {
  id: string;
  ordinal: number;
  /** Full chunk text as stored (backend field name is "content", not a truncated preview —
   * truncation for display is done client-side, see ChunksTab). */
  content: string;
  tokenCount: number;
}

export interface KnowledgeDocumentVersionDto {
  version: number;
  embeddingModel: string;
  chunkCount: number;
  createdAt: string;
}

/**
 * Shape returned by the DETAIL endpoint (GetKnowledgeDocumentByIdQuery →
 * KnowledgeDocumentDetailDto). Distinct field set from the list DTO: has sourceUrl/
 * embeddingModel/embeddingDimensions/chunkTotal, but no updatedAt (the detail query never
 * selects it).
 */
export interface KnowledgeDocumentDetailDto {
  id: string;
  title: string;
  fileName: string;
  sourceType: SourceType;
  sourceUrl: string | null;
  status: IngestionStatus;
  chunkCount: number;
  failureReason: string | null;
  version: number;
  embeddingModel: string;
  embeddingDimensions: number;
  createdAt: string;
  chunks: KnowledgeChunkDto[];
  chunkTotal: number;
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
  /** Q&A pairs serialised as JSON when sourceType is 'Faq' */
  faqPairs?: string;
  dateFrom?: string;
  dateTo?: string;
}
