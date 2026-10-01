using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_quotas",
                columns: table => new
                {
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequestsPerMinute = table.Column<int>(type: "integer", nullable: false),
                    MaxTokensPerDay = table.Column<long>(type: "bigint", nullable: false),
                    MaxDailyBudgetUsd = table.Column<double>(type: "double precision", nullable: false),
                    CurrentDailyTokens = table.Column<long>(type: "bigint", nullable: false),
                    CurrentDailyCostUsd = table.Column<double>(type: "double precision", nullable: false),
                    CurrentDailyRequests = table.Column<int>(type: "integer", nullable: false),
                    LastResetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_quotas", x => x.TenantId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_quotas");
        }
    }
}
