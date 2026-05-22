using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomOnnxInferenceJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_onnx_inference_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    model_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    input_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    output_image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    latency_ms = table.Column<long>(type: "bigint", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_onnx_inference_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_custom_onnx_inference_jobs_custom_onnx_models_model_id",
                        column: x => x.model_id,
                        principalTable: "custom_onnx_models",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_inference_jobs_model_id",
                table: "custom_onnx_inference_jobs",
                column: "model_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_inference_jobs_status",
                table: "custom_onnx_inference_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_custom_onnx_inference_jobs_tenant_id",
                table: "custom_onnx_inference_jobs",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_onnx_inference_jobs");
        }
    }
}
