using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvertaItpSectionScoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_score_band_mappings_program_id_min_raw",
                table: "score_band_mappings");

            migrationBuilder.AlterColumn<string>(
                name: "predicted_band",
                table: "score_band_mappings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "scaled_score",
                table: "score_band_mappings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "section",
                table: "score_band_mappings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_score_band_mappings_program_id_section_min_raw",
                table: "score_band_mappings",
                columns: new[] { "program_id", "section", "min_raw" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_score_band_mappings_program_id_section_min_raw",
                table: "score_band_mappings");

            migrationBuilder.DropColumn(
                name: "scaled_score",
                table: "score_band_mappings");

            migrationBuilder.DropColumn(
                name: "section",
                table: "score_band_mappings");

            migrationBuilder.AlterColumn<string>(
                name: "predicted_band",
                table: "score_band_mappings",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_score_band_mappings_program_id_min_raw",
                table: "score_band_mappings",
                columns: new[] { "program_id", "min_raw" });
        }
    }
}
