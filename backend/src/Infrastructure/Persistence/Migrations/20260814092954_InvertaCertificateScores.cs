using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvertaCertificateScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scaled_scores",
                table: "certificates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_scaled_score",
                table: "certificates",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scaled_scores",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "total_scaled_score",
                table: "certificates");
        }
    }
}
