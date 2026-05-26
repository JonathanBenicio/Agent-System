using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancyToCoreEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "prompt_templates",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "eval_suite_results",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AgentPolicies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "agent_versions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "eval_suite_results");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "agent_versions");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "AgentPolicies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
