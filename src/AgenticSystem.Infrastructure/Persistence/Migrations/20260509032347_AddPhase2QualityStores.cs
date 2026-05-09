using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2QualityStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ix_eval_suite_results_agent_name",
                table: "eval_suite_results",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_eval_suite_results_started_at",
                table: "eval_suite_results",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_prompt_templates_agent_locale_active",
                table: "prompt_templates",
                columns: new[] { "agent_name", "locale", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_prompt_templates_agent_name",
                table: "prompt_templates",
                column: "agent_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_versions");

            migrationBuilder.DropTable(
                name: "eval_suite_results");

            migrationBuilder.DropTable(
                name: "prompt_templates");
        }
    }
}
