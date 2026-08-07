using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Knowledge.Infrastructure.Migrations
{
    /// <summary>
    /// The knowledge schema, relocated from the Chat service (Standard 5: database-per-service)
    /// with the embedding column at vector(1024) — the fixed dimension of BGE-M3 and Cohere
    /// embed-multilingual-v3.0 (CHATBOT-ARCHITECTURE.md §12/§13).
    /// Hand-written SQL: RLS policies + grants can't be expressed by the EF model, and columns
    /// not yet mapped by the entities (source_type, title, ...) carry defaults so mapped inserts
    /// succeed. The HNSW index ships in a separate migration (can't share this DDL transaction).
    /// </summary>
    public partial class InitialKnowledgeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE knowledge_documents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_type smallint NOT NULL DEFAULT 0,
    title text NOT NULL DEFAULT '',
    ingestion_status smallint NOT NULL DEFAULT 0,
    embedding_model text NOT NULL DEFAULT 'BAAI/bge-m3',
    embedding_dimensions int NOT NULL DEFAULT 1024,
    chunk_count int NOT NULL DEFAULT 0,
    content_hash text NOT NULL,
    failure_reason text,
    version int NOT NULL DEFAULT 1,
    supersedes_document_id uuid,
    is_active boolean NOT NULL DEFAULT true,
    source_url text,
    original_file_name text,
    created_by_user_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_knowledge_documents_content_hash
    ON knowledge_documents (tenant_id, content_hash);

CREATE INDEX idx_knowledge_documents_tenant
    ON knowledge_documents (tenant_id);

ALTER TABLE knowledge_documents ENABLE ROW LEVEL SECURITY;
ALTER TABLE knowledge_documents FORCE ROW LEVEL SECURITY;

CREATE POLICY ""TenantIsolation"" ON knowledge_documents
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON knowledge_documents TO nexconvo_service;

CREATE TABLE knowledge_chunks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    knowledge_document_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    ordinal int NOT NULL DEFAULT 0,
    content text NOT NULL,
    token_count int NOT NULL DEFAULT 0,
    document_version int NOT NULL DEFAULT 1,
    embedding_model text NOT NULL DEFAULT 'BAAI/bge-m3',
    embedding vector(1024),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT knowledge_chunks_knowledge_document_id_fkey
        FOREIGN KEY (knowledge_document_id) REFERENCES knowledge_documents(id) ON DELETE CASCADE
);

CREATE INDEX idx_knowledge_chunks_tenant
    ON knowledge_chunks (tenant_id);

CREATE INDEX idx_knowledge_chunks_document
    ON knowledge_chunks (tenant_id, knowledge_document_id);

ALTER TABLE knowledge_chunks ENABLE ROW LEVEL SECURITY;
ALTER TABLE knowledge_chunks FORCE ROW LEVEL SECURITY;

CREATE POLICY ""TenantIsolation"" ON knowledge_chunks
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON knowledge_chunks TO nexconvo_service;

CREATE TABLE knowledge_audit_logs (
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    user_id uuid,
    action text NOT NULL,
    detail text,
    occurred_at timestamptz NOT NULL
);

CREATE INDEX idx_knowledge_audit_logs_tenant_id
    ON knowledge_audit_logs (tenant_id);

CREATE INDEX idx_knowledge_audit_logs_occurred_at
    ON knowledge_audit_logs (occurred_at);

ALTER TABLE knowledge_audit_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE knowledge_audit_logs FORCE ROW LEVEL SECURITY;

CREATE POLICY ""TenantIsolation"" ON knowledge_audit_logs
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON knowledge_audit_logs TO nexconvo_service;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
REVOKE ALL ON knowledge_audit_logs FROM nexconvo_service;
REVOKE ALL ON knowledge_chunks FROM nexconvo_service;
REVOKE ALL ON knowledge_documents FROM nexconvo_service;

DROP TABLE IF EXISTS knowledge_audit_logs;
DROP TABLE IF EXISTS knowledge_chunks;
DROP TABLE IF EXISTS knowledge_documents;
");
        }
    }
}
