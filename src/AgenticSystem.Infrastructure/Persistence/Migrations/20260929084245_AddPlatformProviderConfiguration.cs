using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformProviderConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_config_audits",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    previous_value_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    new_value_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_config_audits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_configs",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    encrypted_value = table.Column<string>(type: "text", nullable: true),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_configs", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_config_audits_key_changed_at",
                table: "platform_config_audits",
                columns: new[] { "key", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_config_audits");

            migrationBuilder.DropTable(
                name: "platform_configs");
        }
    }
}
