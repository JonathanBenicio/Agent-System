using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "workflow_step_executions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "workflow_executions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "workflow_definitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "EmbeddingData",
                table: "vector_documents",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "vector_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "trigger_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "scheduled_tasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "scheduled_task_executions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "runtime_reflections",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "runtime_metrics_snapshots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "runtime_evaluations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "runtime_artifacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "role_assignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "outbox_messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "model_performance_records",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "migration_jobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "knowledge_graph_nodes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "knowledge_graph_edges",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "enhanced_memories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "embedding_models",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "data_connectors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "config_entries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "config_change_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "audit_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "agent_performance_metrics",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "agent_memories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ExternalProviderQuotas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    ApiKeyId = table.Column<string>(type: "text", nullable: false),
                    LimitRequests = table.Column<long>(type: "bigint", nullable: false),
                    RemainingRequests = table.Column<long>(type: "bigint", nullable: false),
                    LimitTokens = table.Column<long>(type: "bigint", nullable: false),
                    RemainingTokens = table.Column<long>(type: "bigint", nullable: false),
                    ResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalBalance = table.Column<double>(type: "double precision", nullable: false),
                    BalanceRemaining = table.Column<double>(type: "double precision", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalProviderQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboundWebhooks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Secret = table.Column<string>(type: "text", nullable: false),
                    TargetWorkflowId = table.Column<string>(type: "text", nullable: true),
                    TargetAgentName = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundWebhooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpPlugins",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    AutoStart = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpPlugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAlerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: true),
                    Percentage = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAlerts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalProviderQuotas");

            migrationBuilder.DropTable(
                name: "InboundWebhooks");

            migrationBuilder.DropTable(
                name: "McpPlugins");

            migrationBuilder.DropTable(
                name: "SystemAlerts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "workflow_step_executions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "workflow_definitions");

            migrationBuilder.DropColumn(
                name: "EmbeddingData",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "trigger_rules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "scheduled_tasks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "scheduled_task_executions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "runtime_reflections");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "runtime_metrics_snapshots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "runtime_evaluations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "runtime_artifacts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "model_performance_records");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "migration_jobs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "knowledge_graph_nodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "knowledge_graph_edges");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "enhanced_memories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "embedding_models");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "config_entries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "config_change_logs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "agent_performance_metrics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "agent_memories");

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "role_assignments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "data_connectors",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "audit_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
        }
    }
}
