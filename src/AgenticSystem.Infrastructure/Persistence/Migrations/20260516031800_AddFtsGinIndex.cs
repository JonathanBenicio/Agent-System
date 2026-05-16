using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFtsGinIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create a GIN index on the vector_documents table for PostgreSQL Full-Text Search.
            // This powers the HybridSearchAsync method (Phase 4: Enterprise Scale).
            // The index is on to_tsvector('english', content) enabling fast ts_rank queries.
            // NOTE: This index is built CONCURRENTLY to avoid table lock during deployment (zero-downtime).
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vector_documents_fts
                ON vector_documents
                USING GIN (to_tsvector('english', content));
                """, suppressTransaction: true);

            // Optional: a partial index for non-null content to avoid indexing empty rows.
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_vector_documents_fts_type
                ON vector_documents
                USING GIN (to_tsvector('english', coalesce(content, '') || ' ' || coalesce(type, '')));
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_vector_documents_fts;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_vector_documents_fts_type;");
        }
    }
}
