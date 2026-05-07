using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateEfPersistenceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_memories",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    memory_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    keywords = table.Column<string>(type: "jsonb", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usage_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_memories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_performance_metrics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    domain = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    user_satisfaction = table.Column<double>(type: "double precision", nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_performance_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_change_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    config_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    previous_value_hash = table.Column<string>(type: "text", nullable: true),
                    new_value_hash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_change_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    encrypted_value = table.Column<string>(type: "text", nullable: true),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_budgets",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(384)", maxLength: 384, nullable: false),
                    service_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    daily_budget = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_budgets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "embedding_models",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_models", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "migration_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reranking_assets",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    asset_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reranking_assets", x => new { x.tenant_id, x.asset_type });
                });

            migrationBuilder.CreateTable(
                name: "runtime_artifacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    related_ids = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runtime_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runtime_evaluations",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    overall_score = table.Column<double>(type: "double precision", nullable: false),
                    baseline_score = table.Column<double>(type: "double precision", nullable: false),
                    threshold = table.Column<double>(type: "double precision", nullable: false),
                    regression_detected = table.Column<bool>(type: "boolean", nullable: false),
                    factors = table.Column<string>(type: "jsonb", nullable: false),
                    alerts = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runtime_evaluations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runtime_metrics_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    stream_count = table.Column<long>(type: "bigint", nullable: false),
                    agent_executions = table.Column<long>(type: "bigint", nullable: false),
                    agent_fallbacks = table.Column<long>(type: "bigint", nullable: false),
                    tool_executions = table.Column<long>(type: "bigint", nullable: false),
                    tool_approvals_requested = table.Column<long>(type: "bigint", nullable: false),
                    tool_approvals_resolved = table.Column<long>(type: "bigint", nullable: false),
                    final_approvals_requested = table.Column<long>(type: "bigint", nullable: false),
                    final_approvals_resolved = table.Column<long>(type: "bigint", nullable: false),
                    handoffs = table.Column<long>(type: "bigint", nullable: false),
                    rag_queries = table.Column<long>(type: "bigint", nullable: false),
                    reviews = table.Column<long>(type: "bigint", nullable: false),
                    avg_agent_latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    avg_tool_latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    events_by_type = table.Column<string>(type: "jsonb", nullable: false),
                    agent_execution_counts = table.Column<string>(type: "jsonb", nullable: false),
                    snapshot_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runtime_metrics_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runtime_reflections",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action_taken = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    deviations = table.Column<string>(type: "jsonb", nullable: false),
                    lessons_learned = table.Column<string>(type: "jsonb", nullable: false),
                    improvement_suggestion = table.Column<string>(type: "text", nullable: true),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runtime_reflections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_task_executions",
                columns: table => new
                {
                    execution_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    task_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_task_executions", x => x.execution_id);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    next_run_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_consolidated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    limits = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_api_keys = table.Column<string>(type: "jsonb", nullable: false),
                    settings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trigger_rules",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trigger_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vector_documents",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    collection = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    embedding = table.Column<float[]>(type: "real[]", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    indexed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vector_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_memories_agent_name",
                table: "agent_memories",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_memories_last_used_at",
                table: "agent_memories",
                column: "last_used_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_memories_user_agent",
                table: "agent_memories",
                columns: new[] { "user_id", "agent_name" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_memories_user_id",
                table: "agent_memories",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_metrics_agent_domain",
                table: "agent_performance_metrics",
                columns: new[] { "agent_name", "domain" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_metrics_agent_name",
                table: "agent_performance_metrics",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_metrics_domain",
                table: "agent_performance_metrics",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "ix_config_change_logs_key_changed_at",
                table: "config_change_logs",
                columns: new[] { "config_key", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_config_entries_category",
                table: "config_entries",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_config_entries_key",
                table: "config_entries",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_budgets_tenant_service",
                table: "cost_budgets",
                columns: new[] { "tenant_id", "service_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cost_entries_recorded_at",
                table: "cost_entries",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_cost_entries_service_name",
                table: "cost_entries",
                column: "service_name");

            migrationBuilder.CreateIndex(
                name: "ix_cost_entries_tenant_id",
                table: "cost_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_cost_entries_tenant_service_date",
                table: "cost_entries",
                columns: new[] { "tenant_id", "service_name", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "idx_embedding_models_active",
                table: "embedding_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_embedding_models_name",
                table: "embedding_models",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_migration_jobs_created",
                table: "migration_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_migration_jobs_status",
                table: "migration_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_artifacts_created_at",
                table: "runtime_artifacts",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_artifacts_session_id",
                table: "runtime_artifacts",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_artifacts_session_type",
                table: "runtime_artifacts",
                columns: new[] { "session_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_runtime_artifacts_type",
                table: "runtime_artifacts",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_evaluations_agent_name",
                table: "runtime_evaluations",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_evaluations_created_at",
                table: "runtime_evaluations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_evaluations_regression",
                table: "runtime_evaluations",
                column: "regression_detected");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_metrics_session_id",
                table: "runtime_metrics_snapshots",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_metrics_snapshot_at",
                table: "runtime_metrics_snapshots",
                column: "snapshot_at");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_reflections_agent_name",
                table: "runtime_reflections",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_reflections_created_at",
                table: "runtime_reflections",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_runtime_reflections_session_id",
                table: "runtime_reflections",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_task_executions_task_id",
                table: "scheduled_task_executions",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_tasks_status",
                table: "scheduled_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_started",
                table: "sessions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_tenant_id",
                table: "sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sessions_user_id",
                table: "sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_is_active",
                table: "tenants",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trigger_rules_enabled",
                table: "trigger_rules",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "ix_vector_documents_collection",
                table: "vector_documents",
                column: "collection");

            migrationBuilder.CreateIndex(
                name: "ix_vector_documents_indexed_at",
                table: "vector_documents",
                column: "indexed_at");

            migrationBuilder.CreateIndex(
                name: "ix_vector_documents_type",
                table: "vector_documents",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_memories");

            migrationBuilder.DropTable(
                name: "agent_performance_metrics");

            migrationBuilder.DropTable(
                name: "config_change_logs");

            migrationBuilder.DropTable(
                name: "config_entries");

            migrationBuilder.DropTable(
                name: "cost_budgets");

            migrationBuilder.DropTable(
                name: "cost_entries");

            migrationBuilder.DropTable(
                name: "embedding_models");

            migrationBuilder.DropTable(
                name: "migration_jobs");

            migrationBuilder.DropTable(
                name: "reranking_assets");

            migrationBuilder.DropTable(
                name: "runtime_artifacts");

            migrationBuilder.DropTable(
                name: "runtime_evaluations");

            migrationBuilder.DropTable(
                name: "runtime_metrics_snapshots");

            migrationBuilder.DropTable(
                name: "runtime_reflections");

            migrationBuilder.DropTable(
                name: "scheduled_task_executions");

            migrationBuilder.DropTable(
                name: "scheduled_tasks");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "trigger_rules");

            migrationBuilder.DropTable(
                name: "vector_documents");
        }
    }
}
