using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Persistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    tool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model_used = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cost = table.Column<decimal>(type: "numeric", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    role_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    granted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_tenant_id",
                table: "audit_entries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_timestamp",
                table: "audit_entries",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_user_id",
                table: "audit_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_created_at",
                table: "outbox_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_tenant_id",
                table: "role_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_unique",
                table: "role_assignments",
                columns: new[] { "user_id", "role_id", "tenant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "role_assignments");
        }
    }
}
