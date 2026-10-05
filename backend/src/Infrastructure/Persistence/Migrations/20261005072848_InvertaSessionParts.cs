using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvertaSessionParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress");

            migrationBuilder.AddColumn<Guid>(
                name: "part_id",
                table: "watch_progress",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "session_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    provider_asset_id = table.Column<string>(type: "text", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_parts", x => x.id);
                    table.CheckConstraint("ck_session_parts_test", "kind <> 'Test' OR (assessment_id IS NOT NULL AND provider_asset_id IS NULL)");
                    table.CheckConstraint("ck_session_parts_video", "kind = 'Test' OR (provider_asset_id IS NOT NULL AND assessment_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_session_parts_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_parts_program_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "program_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_part_id",
                table: "watch_progress",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_part_id",
                table: "watch_progress",
                columns: new[] { "user_id", "part_id" },
                unique: true,
                filter: "part_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress",
                columns: new[] { "user_id", "session_id" },
                filter: "session_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_parts_assessment_id",
                table: "session_parts",
                column: "assessment_id",
                unique: true,
                filter: "assessment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_parts_session_id_order_index",
                table: "session_parts",
                columns: new[] { "session_id", "order_index" });

            migrationBuilder.AddForeignKey(
                name: "fk_watch_progress_session_parts_part_id",
                table: "watch_progress",
                column: "part_id",
                principalTable: "session_parts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(SessionPartBackfill.Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_watch_progress_session_parts_part_id",
                table: "watch_progress");

            migrationBuilder.DropTable(
                name: "session_parts");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_part_id",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_part_id",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress");

            migrationBuilder.DropColumn(
                name: "part_id",
                table: "watch_progress");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress",
                columns: new[] { "user_id", "session_id" },
                unique: true,
                filter: "session_id IS NOT NULL");
        }
    }
}
