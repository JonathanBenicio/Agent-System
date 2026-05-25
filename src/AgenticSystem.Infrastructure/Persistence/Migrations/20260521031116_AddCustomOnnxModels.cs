using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomOnnxModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_onnx_models",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    model_file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    model_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    input_node_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    output_node_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_width = table.Column<int>(type: "integer", nullable: false),
                    input_height = table.Column<int>(type: "integer", nullable: false),
                    channels = table.Column<int>(type: "integer", nullable: false),
                    scale_factor = table.Column<float>(type: "real", nullable: false),
                    mean_red = table.Column<float>(type: "real", nullable: false),
                    mean_green = table.Column<float>(type: "real", nullable: false),
                    mean_blue = table.Column<float>(type: "real", nullable: false),
                    output_format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    post_process_config = table.Column<string>(type: "jsonb", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_models", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_models_active",
                table: "custom_onnx_models",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_models_tenant_id",
                table: "custom_onnx_models",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_custom_onnx_models_tenant_name",
                table: "custom_onnx_models",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_onnx_models");
        }
    }
}
