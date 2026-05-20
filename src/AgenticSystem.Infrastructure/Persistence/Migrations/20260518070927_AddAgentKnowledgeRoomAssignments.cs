using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentKnowledgeRoomAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_knowledge_room_assignments",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    room_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_knowledge_room_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_room_assignments_agent",
                table: "agent_knowledge_room_assignments",
                column: "agent_name");

            migrationBuilder.CreateIndex(
                name: "ix_agent_room_assignments_tenant",
                table: "agent_knowledge_room_assignments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_agent_room_assignment",
                table: "agent_knowledge_room_assignments",
                columns: new[] { "agent_name", "room_id", "tenant_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_knowledge_room_assignments");
        }
    }
}
