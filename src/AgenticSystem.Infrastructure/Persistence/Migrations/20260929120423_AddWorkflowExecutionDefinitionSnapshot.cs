using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowExecutionDefinitionSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "workflow_definition_hash",
                table: "workflow_executions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workflow_definition_snapshot",
                table: "workflow_executions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "workflow_definition_version",
                table: "workflow_executions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "workflow_definition_hash",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "workflow_definition_snapshot",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "workflow_definition_version",
                table: "workflow_executions");
        }
    }
}
