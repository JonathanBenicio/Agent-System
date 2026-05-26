using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "access_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, defaultValue: "Admin"),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_knowledge_room_assignments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    room_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_knowledge_room_assignments", x => x.id);
                });

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
                name: "agent_memories",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                name: "agent_skills",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    system_prompt_fragment = table.Column<string>(type: "text", nullable: false),
                    few_shot_examples = table.Column<string>(type: "text", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    environment = table.Column<int>(type: "integer", nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    model_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tools = table.Column<List<string>>(type: "text[]", nullable: false),
                    policy_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    change_log = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    promoted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    promoted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    config_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    parent_version_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AgentPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AgentNamePattern = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    MaxAutonomyLevel = table.Column<int>(type: "integer", nullable: false),
                    AllowedToolCategories = table.Column<List<string>>(type: "text[]", nullable: false),
                    DeniedTools = table.Column<List<string>>(type: "text[]", nullable: false),
                    AllowedProviders = table.Column<List<string>>(type: "text[]", nullable: false),
                    MaxCostPerRequest = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxCostPerDay = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaxTokensPerRequest = table.Column<int>(type: "integer", nullable: true),
                    RequireFinalApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovalThreshold = table.Column<int>(type: "integer", nullable: false),
                    ContentFilters = table.Column<List<string>>(type: "text[]", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_used = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cost = table.Column<decimal>(type: "numeric", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_change_logs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                name: "custom_onnx_models",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    model_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    model_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    input_node_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    output_node_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_width = table.Column<int>(type: "integer", nullable: false),
                    input_height = table.Column<int>(type: "integer", nullable: false),
                    channels = table.Column<int>(type: "integer", nullable: false),
                    scale_factor = table.Column<float>(type: "real", nullable: false),
                    mean_red = table.Column<float>(type: "real", nullable: false),
                    mean_green = table.Column<float>(type: "real", nullable: false),
                    mean_blue = table.Column<float>(type: "real", nullable: false),
                    output_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    post_process_config = table.Column<string>(type: "jsonb", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_models", x => x.id);
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
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                name: "dynamic_agents",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    autonomy_level = table.Column<int>(type: "integer", nullable: false),
                    allowed_tools_json = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dynamic_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "embedding_models",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                name: "enhanced_memories",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                name: "eval_suite_results",
                columns: table => new
                {
                    suite_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    agent_version_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    total_tests = table.Column<int>(type: "integer", nullable: false),
                    passed = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    overall_score = table.Column<double>(type: "double precision", nullable: false),
                    accuracy_score = table.Column<double>(type: "double precision", nullable: false),
                    safety_score = table.Column<double>(type: "double precision", nullable: false),
                    latency_p50_ms = table.Column<double>(type: "double precision", nullable: false),
                    latency_p95_ms = table.Column<double>(type: "double precision", nullable: false),
                    total_tokens_used = table.Column<int>(type: "integer", nullable: false),
                    results = table.Column<string>(type: "jsonb", nullable: false),
                    regressions = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eval_suite_results", x => x.suite_id);
                });

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
                name: "knowledge_graph_edges",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "knowledge_room_permissions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    room_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_room_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_rooms",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    document_count = table.Column<int>(type: "integer", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "llm_provider_api_keys",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    encrypted_value = table.Column<string>(type: "text", nullable: false),
                    last_four = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    models = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_provider_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LlmPricingRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    CostPerMillionPromptTokens = table.Column<decimal>(type: "numeric", nullable: false),
                    CostPerMillionCompletionTokens = table.Column<decimal>(type: "numeric", nullable: false),
                    CostPerMillionCachedTokens = table.Column<decimal>(type: "numeric", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmPricingRules", x => x.Id);
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
                name: "migration_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "model_performance_records",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prompt_templates",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    template_body = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    variables = table.Column<List<string>>(type: "text[]", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_templates", x => x.id);
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
                name: "role_assignments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    role_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "runtime_artifacts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                name: "SessionInsights",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    FactsJson = table.Column<string>(type: "text", nullable: false),
                    DecisionsJson = table.Column<string>(type: "text", nullable: false),
                    PreferencesJson = table.Column<string>(type: "text", nullable: false),
                    ActionItemsJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionInsights", x => x.Id);
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
                name: "SessionSummaries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TopicsJson = table.Column<string>(type: "text", nullable: false),
                    AgentsJson = table.Column<string>(type: "text", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionDuration = table.Column<TimeSpan>(type: "interval", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_states",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_states", x => x.id);
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    collection = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: true),
                    EmbeddingData = table.Column<byte[]>(type: "bytea", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    indexed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "content" }),
                    ContextualSummary = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vector_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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
                    TenantId = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "custom_onnx_inference_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    input_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    output_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    latency_ms = table.Column<long>(type: "bigint", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_inference_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_onnx_inference_jobs_custom_onnx_models_model_id",
                        column: x => x.model_id,
                        principalTable: "custom_onnx_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_onnx_model_files",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_model_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_onnx_model_files_custom_onnx_models_model_id",
                        column: x => x.model_id,
                        principalTable: "custom_onnx_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_api_keys_hash",
                table: "access_api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_api_keys_tenant_id",
                table: "access_api_keys",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_room_assignments_agent",
                table: "agent_knowledge_room_assignments",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_room_assignments_tenant",
                table: "agent_knowledge_room_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_agent_room_assignment",
                table: "agent_knowledge_room_assignments",
                columns: new[] { "agent_name", "room_id", "tenant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_author",
                table: "agent_marketplace_entries",
                column: "author");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_domain",
                table: "agent_marketplace_entries",
                column: "domain");

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
                name: "ix_agent_skills_tenant_id",
                table: "agent_skills",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_versions_agent_name",
                table: "agent_versions",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_versions_agent_version_unique",
                table: "agent_versions",
                columns: new[] { "agent_name", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id",
                table: "audit_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_timestamp",
                table: "audit_entries",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_user_id",
                table: "audit_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_config_change_logs_key_changed_at",
                table: "config_change_logs",
                columns: new[] { "config_key", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_config_entries_category",
                table: "config_entries",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_config_entries_tenant_key",
                table: "config_entries",
                columns: new[] { "tenant_id", "key" },
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
                name: "ix_custom_onnx_inference_jobs_model_id",
                table: "custom_onnx_inference_jobs",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_inference_jobs_status",
                table: "custom_onnx_inference_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_inference_jobs_tenant_id",
                table: "custom_onnx_inference_jobs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_model_files_model_id",
                table: "custom_onnx_model_files",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_model_files_tenant_id",
                table: "custom_onnx_model_files",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_custom_onnx_model_files_model_name",
                table: "custom_onnx_model_files",
                columns: new[] { "model_id", "file_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_models_active",
                table: "custom_onnx_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_models_tenant_id",
                table: "custom_onnx_models",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_custom_onnx_models_tenant_name",
                table: "custom_onnx_models",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_connectors_tenant_id",
                table: "data_connectors",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dynamic_agents_tenant_id",
                table: "dynamic_agents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "idx_embedding_models_active",
                table: "embedding_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "idx_embedding_models_name",
                table: "embedding_models",
                column: "name");

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
                name: "ix_eval_suite_results_agent_name",
                table: "eval_suite_results",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_eval_suite_results_started_at",
                table: "eval_suite_results",
                column: "started_at");

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

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_room_permissions_room_user",
                table: "knowledge_room_permissions",
                columns: new[] { "room_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_room_permissions_tenant_id",
                table: "knowledge_room_permissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_rooms_tenant_id",
                table: "knowledge_rooms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_llm_api_keys_tenant_provider",
                table: "llm_provider_api_keys",
                columns: new[] { "tenant_id", "provider_name" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_api_keys_tenant_provider_default",
                table: "llm_provider_api_keys",
                columns: new[] { "tenant_id", "provider_name", "is_default" });

            migrationBuilder.CreateIndex(
                name: "ux_llm_api_keys_tenant_provider_name",
                table: "llm_provider_api_keys",
                columns: new[] { "tenant_id", "provider_name", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_migration_jobs_created",
                table: "migration_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_migration_jobs_status",
                table: "migration_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_model_performance_model_id",
                table: "model_performance_records",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_model_performance_recorded_at",
                table: "model_performance_records",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_created_at",
                table: "outbox_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_prompt_templates_agent_locale_active",
                table: "prompt_templates",
                columns: new[] { "agent_name", "locale", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_prompt_templates_agent_name",
                table: "prompt_templates",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_tenant_id",
                table: "role_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_unique",
                table: "role_assignments",
                columns: new[] { "user_id", "role_id", "tenant_id" },
                unique: true);

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
                name: "ix_system_states_tenant_id",
                table: "system_states",
                column: "tenant_id");

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
                name: "IX_vector_documents_SearchVector",
                table: "vector_documents",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_vector_documents_type",
                table: "vector_documents",
                column: "type");

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
                name: "access_api_keys");

            migrationBuilder.DropTable(
                name: "agent_knowledge_room_assignments");

            migrationBuilder.DropTable(
                name: "agent_marketplace_entries");

            migrationBuilder.DropTable(
                name: "agent_memories");

            migrationBuilder.DropTable(
                name: "agent_performance_metrics");

            migrationBuilder.DropTable(
                name: "agent_skills");

            migrationBuilder.DropTable(
                name: "agent_versions");

            migrationBuilder.DropTable(
                name: "AgentPolicies");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "config_change_logs");

            migrationBuilder.DropTable(
                name: "config_entries");

            migrationBuilder.DropTable(
                name: "cost_budgets");

            migrationBuilder.DropTable(
                name: "cost_entries");

            migrationBuilder.DropTable(
                name: "custom_onnx_inference_jobs");

            migrationBuilder.DropTable(
                name: "custom_onnx_model_files");

            migrationBuilder.DropTable(
                name: "data_connectors");

            migrationBuilder.DropTable(
                name: "dynamic_agents");

            migrationBuilder.DropTable(
                name: "embedding_models");

            migrationBuilder.DropTable(
                name: "enhanced_memories");

            migrationBuilder.DropTable(
                name: "eval_suite_results");

            migrationBuilder.DropTable(
                name: "ExternalProviderQuotas");

            migrationBuilder.DropTable(
                name: "InboundWebhooks");

            migrationBuilder.DropTable(
                name: "knowledge_graph_edges");

            migrationBuilder.DropTable(
                name: "knowledge_graph_nodes");

            migrationBuilder.DropTable(
                name: "knowledge_room_permissions");

            migrationBuilder.DropTable(
                name: "knowledge_rooms");

            migrationBuilder.DropTable(
                name: "llm_provider_api_keys");

            migrationBuilder.DropTable(
                name: "LlmPricingRules");

            migrationBuilder.DropTable(
                name: "McpPlugins");

            migrationBuilder.DropTable(
                name: "migration_jobs");

            migrationBuilder.DropTable(
                name: "model_performance_records");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "reranking_assets");

            migrationBuilder.DropTable(
                name: "role_assignments");

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
                name: "SessionInsights");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "SessionSummaries");

            migrationBuilder.DropTable(
                name: "system_states");

            migrationBuilder.DropTable(
                name: "SystemAlerts");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "trigger_rules");

            migrationBuilder.DropTable(
                name: "vector_documents");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropTable(
                name: "workflow_executions");

            migrationBuilder.DropTable(
                name: "workflow_step_executions");

            migrationBuilder.DropTable(
                name: "custom_onnx_models");
        }
    }
}
