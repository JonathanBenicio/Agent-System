using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistSelfImprovementProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "self_improvement_proposals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfidenceLevel = table.Column<double>(type: "double precision", nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    ProposedChangesJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousInstructions = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedPromptVersion = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_self_improvement_proposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_self_improvement_proposals_TenantId_AgentName_CreatedAt",
                table: "self_improvement_proposals",
                columns: new[] { "TenantId", "AgentName", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_self_improvement_proposals_TenantId_Status_CreatedAt",
                table: "self_improvement_proposals",
                columns: new[] { "TenantId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "self_improvement_proposals");
        }
    }
}
