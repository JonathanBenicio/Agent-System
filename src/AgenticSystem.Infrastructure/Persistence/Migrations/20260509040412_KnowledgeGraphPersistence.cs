using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeGraphPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_graph_edges",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    relation_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    weight = table.Column<double>(type: "double precision", nullable: false),
                    properties = table.Column<string>(type: "jsonb", nullable: false),
                    source_document_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_graph_edges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_graph_nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: false),
                    source_document_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_graph_nodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_edges_relation_type",
                table: "knowledge_graph_edges",
                column: "relation_type");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_edges_source_node",
                table: "knowledge_graph_edges",
                column: "source_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_edges_target_node",
                table: "knowledge_graph_edges",
                column: "target_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_edges_unique_relation",
                table: "knowledge_graph_edges",
                columns: new[] { "source_node_id", "target_node_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_nodes_entity_type",
                table: "knowledge_graph_nodes",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_nodes_label",
                table: "knowledge_graph_nodes",
                column: "label");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_graph_nodes_source_document",
                table: "knowledge_graph_nodes",
                column: "source_document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_graph_edges");

            migrationBuilder.DropTable(
                name: "knowledge_graph_nodes");
        }
    }
}
