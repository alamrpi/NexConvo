using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Knowledge.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the column that records where a knowledge document's source bytes live in the
    /// workspace S3 bucket. Every source type (File/Url/Text/Faq) converges on S3 storage
    /// (Slice 3 step 7) so the ingestion job always downloads from one uniform place.
    /// </summary>
    public partial class AddKnowledgeDocumentS3ObjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "s3_object_key",
                table: "knowledge_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "s3_object_key",
                table: "knowledge_documents");
        }
    }
}
