using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePlatformOwnedResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $migration$
                DECLARE
                    tenant_column RECORD;
                    affected_rows BIGINT;
                    unresolved_data TEXT := '';
                BEGIN
                    FOR tenant_column IN
                        SELECT table_schema, table_name, column_name
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND (column_name IN ('TenantId', 'tenant_id')
                               OR (table_name = 'tenants' AND column_name = 'id'))
                    LOOP
                        EXECUTE format(
                            'SELECT count(*) FROM %I.%I WHERE lower(trim(%I::text)) IN (''default'', ''system-devui'')',
                            tenant_column.table_schema,
                            tenant_column.table_name,
                            tenant_column.column_name)
                        INTO affected_rows;

                        IF affected_rows > 0 THEN
                            unresolved_data := concat_ws(
                                ', ',
                                NULLIF(unresolved_data, ''),
                                format('%I.%I.%I: %s row(s)', tenant_column.table_schema,
                                    tenant_column.table_name, tenant_column.column_name, affected_rows));
                        END IF;
                    END LOOP;

                    IF unresolved_data <> '' THEN
                        RAISE EXCEPTION USING
                            MESSAGE = 'Cannot migrate reserved tenant IDs default/system-devui because tenant-owned data still references them.',
                            DETAIL = unresolved_data,
                            HINT = 'Map each row to its approved real tenant or archive it, then retry the migration. Do not promote tenant data to platform scope automatically.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.CreateTable(
                name: "platform_agent_skills",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    domain = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    system_prompt_fragment = table.Column<string>(type: "text", nullable: false),
                    few_shot_examples = table.Column<string>(type: "text", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_agent_skills", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_agent_tools",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requires_auth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    variant_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rollout_percentage = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_agent_tools", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_external_provider_quotas",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    api_key_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    limit_requests = table.Column<long>(type: "bigint", nullable: false),
                    remaining_requests = table.Column<long>(type: "bigint", nullable: false),
                    limit_tokens = table.Column<long>(type: "bigint", nullable: false),
                    remaining_tokens = table.Column<long>(type: "bigint", nullable: false),
                    reset_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_balance = table.Column<double>(type: "double precision", nullable: false),
                    balance_remaining = table.Column<double>(type: "double precision", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_external_provider_quotas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_platform_external_quotas_provider_key",
                table: "platform_external_provider_quotas",
                columns: new[] { "provider_name", "api_key_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_outbox_created_at",
                table: "platform_outbox_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_platform_outbox_processed_at",
                table: "platform_outbox_messages",
                column: "processed_at");

            migrationBuilder.Sql("""
                INSERT INTO platform_agent_tools
                    (id, name, description, category, requires_auth, type, metadata_json, version, variant_name, rollout_percentage, is_default, created_at, updated_at)
                SELECT id, name, description, category, requires_auth, type, COALESCE(metadata_json, '{}'::jsonb), version, variant_name, rollout_percentage, is_default, created_at, updated_at
                FROM agent_tools
                WHERE tenant_id IN ('platform', 'system-background')
                ON CONFLICT (id) DO UPDATE SET
                    name = EXCLUDED.name, description = EXCLUDED.description, category = EXCLUDED.category,
                    requires_auth = EXCLUDED.requires_auth, type = EXCLUDED.type, metadata_json = EXCLUDED.metadata_json,
                    version = EXCLUDED.version, variant_name = EXCLUDED.variant_name,
                    rollout_percentage = EXCLUDED.rollout_percentage, is_default = EXCLUDED.is_default,
                    updated_at = EXCLUDED.updated_at;

                DELETE FROM agent_tools WHERE tenant_id IN ('platform', 'system-background');

                INSERT INTO platform_agent_skills
                    (id, name, domain, type, system_prompt_fragment, few_shot_examples, is_system, is_enabled, metadata_json, created_at, updated_at)
                SELECT id, name, domain, type, system_prompt_fragment, few_shot_examples, is_system, is_enabled, metadata_json, created_at, updated_at
                FROM agent_skills
                WHERE tenant_id IN ('platform', 'system-background')
                ON CONFLICT (id) DO UPDATE SET
                    name = EXCLUDED.name, domain = EXCLUDED.domain, type = EXCLUDED.type,
                    system_prompt_fragment = EXCLUDED.system_prompt_fragment, few_shot_examples = EXCLUDED.few_shot_examples,
                    is_system = EXCLUDED.is_system, is_enabled = EXCLUDED.is_enabled,
                    metadata_json = EXCLUDED.metadata_json, updated_at = EXCLUDED.updated_at;

                DELETE FROM agent_skills WHERE tenant_id IN ('platform', 'system-background');

                INSERT INTO platform_external_provider_quotas
                    (id, provider_name, api_key_id, limit_requests, remaining_requests, limit_tokens, remaining_tokens,
                     reset_at, total_balance, balance_remaining, currency, last_sync_at)
                SELECT DISTINCT ON ("ProviderName", "ApiKeyId")
                       "Id", "ProviderName", "ApiKeyId", "LimitRequests", "RemainingRequests", "LimitTokens", "RemainingTokens",
                       "ResetAt", "TotalBalance", "BalanceRemaining", "Currency", "LastSyncAt"
                FROM "ExternalProviderQuotas"
                WHERE "TenantId" IN ('platform', 'system-background')
                ORDER BY "ProviderName", "ApiKeyId", "LastSyncAt" DESC
                ON CONFLICT (provider_name, api_key_id) DO UPDATE SET
                    id = EXCLUDED.id,
                    limit_requests = EXCLUDED.limit_requests, remaining_requests = EXCLUDED.remaining_requests,
                    limit_tokens = EXCLUDED.limit_tokens, remaining_tokens = EXCLUDED.remaining_tokens,
                    reset_at = EXCLUDED.reset_at, total_balance = EXCLUDED.total_balance,
                    balance_remaining = EXCLUDED.balance_remaining, currency = EXCLUDED.currency, last_sync_at = EXCLUDED.last_sync_at;

                DELETE FROM "ExternalProviderQuotas" WHERE "TenantId" IN ('platform', 'system-background');

                UPDATE outbox_messages AS message
                SET "TenantId" = COALESCE(message.payload->>'TenantId', message.payload->>'tenantId')
                WHERE message."TenantId" IN ('platform', 'system-background')
                  AND EXISTS (
                      SELECT 1 FROM tenants AS tenant
                      WHERE tenant.id = COALESCE(message.payload->>'TenantId', message.payload->>'tenantId')
                        AND tenant.id NOT IN ('platform', 'system-background'));

                INSERT INTO platform_outbox_messages (id, event_type, payload, created_at, processed_at, error)
                SELECT id, event_type,
                       (payload - 'TenantId' - 'tenantId') || '{"TenantId":null,"tenantId":null}'::jsonb,
                       created_at, processed_at, error
                FROM outbox_messages
                WHERE "TenantId" IN ('platform', 'system-background');

                DELETE FROM outbox_messages WHERE "TenantId" IN ('platform', 'system-background');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO agent_tools
                    (id, tenant_id, name, description, category, requires_auth, type, metadata_json, version, variant_name, rollout_percentage, is_default, created_at, updated_at)
                SELECT id, 'platform', name, description, category, requires_auth, type, metadata_json, version, variant_name, rollout_percentage, is_default, created_at, updated_at
                FROM platform_agent_tools ON CONFLICT (id) DO NOTHING;

                INSERT INTO agent_skills
                    (id, tenant_id, name, domain, type, system_prompt_fragment, few_shot_examples, is_system, is_enabled, metadata_json, created_at, updated_at)
                SELECT id, 'platform', name, domain, type, system_prompt_fragment, few_shot_examples, is_system, is_enabled, metadata_json, created_at, updated_at
                FROM platform_agent_skills ON CONFLICT (id) DO NOTHING;

                INSERT INTO "ExternalProviderQuotas"
                    ("Id", "TenantId", "ProviderName", "ApiKeyId", "LimitRequests", "RemainingRequests", "LimitTokens", "RemainingTokens",
                     "ResetAt", "TotalBalance", "BalanceRemaining", "Currency", "LastSyncAt")
                SELECT id, 'platform', provider_name, api_key_id, limit_requests, remaining_requests, limit_tokens, remaining_tokens,
                       reset_at, total_balance, balance_remaining, currency, last_sync_at
                FROM platform_external_provider_quotas ON CONFLICT ("Id") DO NOTHING;

                INSERT INTO outbox_messages (id, "TenantId", event_type, payload, created_at, processed_at, error)
                SELECT id, 'platform', event_type, payload, created_at, processed_at, error
                FROM platform_outbox_messages ON CONFLICT (id) DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "platform_agent_skills");

            migrationBuilder.DropTable(
                name: "platform_agent_tools");

            migrationBuilder.DropTable(
                name: "platform_external_provider_quotas");

            migrationBuilder.DropTable(
                name: "platform_outbox_messages");
        }
    }
}
