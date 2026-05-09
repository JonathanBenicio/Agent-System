using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentPolicies");
        }
    }
}
