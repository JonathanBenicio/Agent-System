using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopedQuotaAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_system_alerts",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    provider_name = table.Column<string>(type: "text", nullable: true),
                    percentage = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_system_alerts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_system_alerts_tenant_created_at",
                table: "tenant_system_alerts",
                columns: new[] { "tenant_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_system_alerts");
        }
    }
}
