using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowExecutionLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "lease_expires_at",
                table: "workflow_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lease_owner",
                table: "workflow_executions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_executions_claim",
                table: "workflow_executions",
                columns: new[] { "status", "lease_expires_at", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workflow_executions_claim",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "lease_owner",
                table: "workflow_executions");
        }
    }
}
