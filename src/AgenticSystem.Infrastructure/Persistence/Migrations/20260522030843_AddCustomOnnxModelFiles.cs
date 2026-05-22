using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomOnnxModelFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_onnx_model_files",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_model_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_onnx_model_files_custom_onnx_models_model_id",
                        column: x => x.model_id,
                        principalTable: "custom_onnx_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_model_files_model_id",
                table: "custom_onnx_model_files",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_model_files_tenant_id",
                table: "custom_onnx_model_files",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_custom_onnx_model_files_model_name",
                table: "custom_onnx_model_files",
                columns: new[] { "model_id", "file_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_onnx_model_files");
        }
    }
}
