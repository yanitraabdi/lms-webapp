using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CertificateOnePerAttempt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates",
                column: "attempt_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates",
                column: "attempt_id");
        }
    }
}
