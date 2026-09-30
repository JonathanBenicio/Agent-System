using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentToolsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_tools",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_agent_tools", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_tools_tenant_id",
                table: "agent_tools",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_tools");
        }
    }
}
