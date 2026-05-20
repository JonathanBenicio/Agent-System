using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixConfigEntryTenantUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_config_entries_key",
                table: "config_entries");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "config_entries",
                newName: "tenant_id");

            migrationBuilder.AlterColumn<string>(
                name: "tenant_id",
                table: "config_entries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_config_entries_tenant_key",
                table: "config_entries",
                columns: new[] { "tenant_id", "key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_config_entries_tenant_key",
                table: "config_entries");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "config_entries",
                newName: "TenantId");

            migrationBuilder.AlterColumn<string>(
                name: "TenantId",
                table: "config_entries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.CreateIndex(
                name: "IX_config_entries_key",
                table: "config_entries",
                column: "key",
                unique: true);
        }
    }
}
