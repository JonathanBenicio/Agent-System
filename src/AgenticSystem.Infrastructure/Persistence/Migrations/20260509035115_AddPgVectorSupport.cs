using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AgenticSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPgVectorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "vector_documents",
                type: "vector",
                nullable: true,
                oldClrType: typeof(float[]),
                oldType: "real[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<float[]>(
                name: "embedding",
                table: "vector_documents",
                type: "real[]",
                nullable: false,
                defaultValue: new float[0],
                oldClrType: typeof(Vector),
                oldType: "vector",
                oldNullable: true);
        }
    }
}
