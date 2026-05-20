using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLLMProviderApiKeysTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "llm_provider_api_keys");
        }
    }
}
