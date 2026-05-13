using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelUpdate_2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_marketplace_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    domain = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    average_rating = table.Column<double>(type: "double precision", nullable: false),
                    install_count = table.Column<int>(type: "integer", nullable: false),
                    specification = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_marketplace_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_connectors",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    connector_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    connection_string = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    settings = table.Column<string>(type: "jsonb", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    sync_schedule = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_connectors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enhanced_memories",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    session_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    memory_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    freshness = table.Column<double>(type: "double precision", nullable: false),
                    decay_rate = table.Column<double>(type: "double precision", nullable: false),
                    access_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tags = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enhanced_memories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "model_performance_records",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    actual_cost_usd = table.Column<double>(type: "double precision", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_performance_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_executions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    variables = table.Column<string>(type: "jsonb", nullable: false),
                    initiated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_executions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_executions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    execution_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    step_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    step_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    output = table.Column<string>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    compensation_executed = table.Column<bool>(type: "boolean", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_step_executions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_author",
                table: "agent_marketplace_entries",
                column: "author");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_domain",
                table: "agent_marketplace_entries",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "ix_data_connectors_tenant_id",
                table: "data_connectors",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_enhanced_memory_agent",
                table: "enhanced_memories",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_enhanced_memory_session",
                table: "enhanced_memories",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_enhanced_memory_type",
                table: "enhanced_memories",
                column: "memory_type");

            migrationBuilder.CreateIndex(
                name: "ix_model_performance_model_id",
                table: "model_performance_records",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_model_performance_recorded_at",
                table: "model_performance_records",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_definitions_name",
                table: "workflow_definitions",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_started_at",
                table: "workflow_executions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_status",
                table: "workflow_executions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_workflow_id",
                table: "workflow_executions",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_execution_id",
                table: "workflow_step_executions",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_step_executions_unique_step",
                table: "workflow_step_executions",
                columns: new[] { "execution_id", "step_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_marketplace_entries");

            migrationBuilder.DropTable(
                name: "data_connectors");

            migrationBuilder.DropTable(
                name: "enhanced_memories");

            migrationBuilder.DropTable(
                name: "model_performance_records");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropTable(
                name: "workflow_executions");

            migrationBuilder.DropTable(
                name: "workflow_step_executions");
        }
    }
}
