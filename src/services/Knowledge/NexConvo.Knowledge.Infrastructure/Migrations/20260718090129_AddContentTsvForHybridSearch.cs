using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NexConvo.Knowledge.Infrastructure.Migrations
{
    /// <summary>
    /// Lexical search support for hybrid retrieval (improve-short-query-retrieval design.md).
    ///
    /// Uses a custom <c>simple_nostop</c> text search configuration — PostgreSQL's <c>simple</c>
    /// dictionary WITHOUT English stopword removal treats "the"/"of"/"about"/"is" as ordinary
    /// searchable tokens, which both (a) lets an unrelated chunk match a query purely because both
    /// happen to contain "the", and (b) makes an OR-style multi-term lexical query too noisy to be
    /// useful. <c>simple_nostop</c> = the <c>simple</c> dictionary's tokenizer/lowercasing, plus the
    /// standard English stopword list, WITHOUT stemming — "refund"/"refunds"/"refunded" still stay
    /// distinct tokens (design.md's documented "simple config, no stemming" decision is preserved;
    /// this only removes grammatical filler words, confirmed to have zero effect on Bengali
    /// tokenization since the stopword list is English-only).
    ///
    /// The generated STORED tsvector column backfills itself from existing content on apply — no
    /// separate backfill job needed. Not mapped as an EF property (deliberately): it is written
    /// only by PostgreSQL itself and read only via KnowledgeChunkRepository's raw SQL, the same way
    /// the embedding column's `<=>` operator has no LINQ-mapped equivalent. GIN index creation is a
    /// separate migration from the column add, mirroring AddKnowledgeChunksHnswIndex's reasoning
    /// that index creation can't share the same DDL transaction as the underlying column.
    /// </summary>
    public partial class AddContentTsvForHybridSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TEXT SEARCH DICTIONARY simple_nostop_dict (
    TEMPLATE = pg_catalog.simple,
    STOPWORDS = english
);

CREATE TEXT SEARCH CONFIGURATION simple_nostop (COPY = simple);
ALTER TEXT SEARCH CONFIGURATION simple_nostop
    ALTER MAPPING FOR asciiword, asciihword, hword_asciipart, word, hword, hword_part
    WITH simple_nostop_dict;

ALTER TABLE knowledge_chunks
    ADD COLUMN content_tsv tsvector
    GENERATED ALWAYS AS (to_tsvector('simple_nostop', content)) STORED;

CREATE INDEX idx_knowledge_chunks_content_tsv
    ON knowledge_chunks
    USING gin (content_tsv);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS idx_knowledge_chunks_content_tsv;
ALTER TABLE knowledge_chunks DROP COLUMN IF EXISTS content_tsv;
DROP TEXT SEARCH CONFIGURATION IF EXISTS simple_nostop;
DROP TEXT SEARCH DICTIONARY IF EXISTS simple_nostop_dict;
");
        }
    }
}
