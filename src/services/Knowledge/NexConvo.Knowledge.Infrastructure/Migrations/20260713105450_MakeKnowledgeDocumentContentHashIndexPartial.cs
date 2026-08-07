using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Knowledge.Infrastructure.Migrations
{
    // The original unique index on (tenant_id, content_hash) covers ALL rows, including
    // soft-deleted ones (is_active = false). UploadKnowledgeDocumentCommandHandler's own dedup
    // check filters on IsActive, so it never treats a soft-deleted document as a duplicate — but
    // the database constraint doesn't know that, so re-uploading identical content after a
    // document was deleted throws a raw DbUpdateException (surfaced as an unhandled 500) instead
    // of creating a new document. Making the index partial (WHERE is_active) scopes uniqueness to
    // only the currently-active rows, matching the application-level check.
    /// <inheritdoc />
    public partial class MakeKnowledgeDocumentContentHashIndexPartial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_knowledge_documents_content_hash",
                table: "knowledge_documents");

            migrationBuilder.CreateIndex(
                name: "idx_knowledge_documents_content_hash",
                table: "knowledge_documents",
                columns: new[] { "tenant_id", "content_hash" },
                unique: true,
                filter: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_knowledge_documents_content_hash",
                table: "knowledge_documents");

            migrationBuilder.CreateIndex(
                name: "idx_knowledge_documents_content_hash",
                table: "knowledge_documents",
                columns: new[] { "tenant_id", "content_hash" },
                unique: true);
        }
    }
}
