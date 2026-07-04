using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Chat.Infrastructure.Migrations;

public partial class AddKnowledgeDocuments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE knowledge_documents (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_type smallint NOT NULL,
    title text NOT NULL,
    ingestion_status smallint NOT NULL DEFAULT 0,
    embedding_model text NOT NULL DEFAULT 'text-embedding-3-small',
    embedding_dimensions int NOT NULL DEFAULT 1536,
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

CREATE POLICY ""TenantIsolation"" ON knowledge_documents
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON knowledge_documents TO nexconvo_service;

CREATE TABLE knowledge_chunks (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    knowledge_document_id uuid NOT NULL REFERENCES knowledge_documents(id) ON DELETE CASCADE,
    tenant_id uuid NOT NULL,
    ordinal int NOT NULL,
    content text NOT NULL,
    token_count int NOT NULL DEFAULT 0,
    embedding_model text NOT NULL,
    embedding vector(1536),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_knowledge_chunks_tenant
    ON knowledge_chunks (tenant_id);

CREATE INDEX idx_knowledge_chunks_document
    ON knowledge_chunks (tenant_id, knowledge_document_id);

ALTER TABLE knowledge_chunks ENABLE ROW LEVEL SECURITY;

CREATE POLICY ""TenantIsolation"" ON knowledge_chunks
    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE, DELETE ON knowledge_chunks TO nexconvo_service;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS knowledge_chunks;
DROP TABLE IF EXISTS knowledge_documents;
");
    }
}
