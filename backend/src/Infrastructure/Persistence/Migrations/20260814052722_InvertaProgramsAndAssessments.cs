using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvertaProgramsAndAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_module_id",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_certificates_user_id_level_id",
                table: "certificates");

            migrationBuilder.AlterColumn<Guid>(
                name: "module_id",
                table: "watch_progress",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                table: "watch_progress",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "level_id",
                table: "certificates",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "attempt_id",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "predicted_band",
                table: "certificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "program_id",
                table: "certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "section_scores",
                table: "certificates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "programs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    price_idr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    choices = table.Column<string>(type: "jsonb", nullable: false),
                    correct = table.Column<string>(type: "jsonb", nullable: false),
                    audio_ref = table.Column<string>(type: "text", nullable: true),
                    passage_ref = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    auto_submitted = table.Column<bool>(type: "boolean", nullable: false),
                    proctor_flagged = table.Column<bool>(type: "boolean", nullable: false),
                    reinstated = table.Column<bool>(type: "boolean", nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    section_scores = table.Column<string>(type: "jsonb", nullable: false),
                    total_score = table.Column<int>(type: "integer", nullable: false),
                    max_score = table.Column<int>(type: "integer", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_attempts_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attempts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_batches_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    provider_asset_id = table.Column<string>(type: "text", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    live_mode = table.Column<string>(type: "text", nullable: true),
                    join_url = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_program_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_program_sessions_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_program_sessions_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "score_band_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_raw = table.Column<int>(type: "integer", nullable: false),
                    max_raw = table.Column<int>(type: "integer", nullable: false),
                    predicted_band = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_band_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_score_band_mappings_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assessment_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_assessment_questions_assessments_assessment_id",
                        column: x => x.assessment_id,
                        principalTable: "assessments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_assessment_questions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "proctor_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    client_meta = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proctor_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_proctor_events_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalTable: "attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    amount_paid_idr = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    provider_ref = table.Column<string>(type: "text", nullable: true),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollments_program_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "program_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_enrollments_programs_program_id",
                        column: x => x.program_id,
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "live_attendances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attended = table.Column<bool>(type: "boolean", nullable: false),
                    marked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_live_attendances", x => x.id);
                    table.ForeignKey(
                        name: "fk_live_attendances_program_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "program_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_live_attendances_users_marked_by_user_id",
                        column: x => x.marked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_live_attendances_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_completions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_completions", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_completions_program_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "program_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_completions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_session_id",
                table: "watch_progress",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_module_id",
                table: "watch_progress",
                columns: new[] { "user_id", "module_id" },
                unique: true,
                filter: "module_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress",
                columns: new[] { "user_id", "session_id" },
                unique: true,
                filter: "session_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_watch_progress_one_target",
                table: "watch_progress",
                sql: "(module_id IS NULL) <> (session_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates",
                column: "attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_program_id",
                table: "certificates",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_user_id_level_id",
                table: "certificates",
                columns: new[] { "user_id", "level_id" },
                unique: true,
                filter: "level_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_certificates_user_id_program_id",
                table: "certificates",
                columns: new[] { "user_id", "program_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assessment_questions_assessment_id_order_index",
                table: "assessment_questions",
                columns: new[] { "assessment_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_questions_question_id",
                table: "assessment_questions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempts_assessment_id",
                table: "attempts",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attempts_user_id_assessment_id",
                table: "attempts",
                columns: new[] { "user_id", "assessment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_batch_id",
                table: "enrollments",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_program_id",
                table: "enrollments",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_provider_ref",
                table: "enrollments",
                column: "provider_ref");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_user_id_program_id",
                table: "enrollments",
                columns: new[] { "user_id", "program_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_attendances_marked_by_user_id",
                table: "live_attendances",
                column: "marked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_attendances_session_id_user_id",
                table: "live_attendances",
                columns: new[] { "session_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_attendances_user_id",
                table: "live_attendances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_proctor_events_attempt_id_occurred_at",
                table: "proctor_events",
                columns: new[] { "attempt_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_program_batches_program_id_start_date",
                table: "program_batches",
                columns: new[] { "program_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_program_sessions_assessment_id",
                table: "program_sessions",
                column: "assessment_id");

            migrationBuilder.CreateIndex(
                name: "ix_program_sessions_program_id_order_index",
                table: "program_sessions",
                columns: new[] { "program_id", "order_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_slug",
                table: "programs",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_section",
                table: "questions",
                column: "section");

            migrationBuilder.CreateIndex(
                name: "ix_score_band_mappings_program_id_min_raw",
                table: "score_band_mappings",
                columns: new[] { "program_id", "min_raw" });

            migrationBuilder.CreateIndex(
                name: "ix_session_completions_session_id",
                table: "session_completions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_completions_user_id_session_id",
                table: "session_completions",
                columns: new[] { "user_id", "session_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_attempts_attempt_id",
                table: "certificates",
                column: "attempt_id",
                principalTable: "attempts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_certificates_programs_program_id",
                table: "certificates",
                column: "program_id",
                principalTable: "programs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_watch_progress_program_sessions_session_id",
                table: "watch_progress",
                column: "session_id",
                principalTable: "program_sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_certificates_attempts_attempt_id",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "fk_certificates_programs_program_id",
                table: "certificates");

            migrationBuilder.DropForeignKey(
                name: "fk_watch_progress_program_sessions_session_id",
                table: "watch_progress");

            migrationBuilder.DropTable(
                name: "assessment_questions");

            migrationBuilder.DropTable(
                name: "enrollments");

            migrationBuilder.DropTable(
                name: "live_attendances");

            migrationBuilder.DropTable(
                name: "proctor_events");

            migrationBuilder.DropTable(
                name: "score_band_mappings");

            migrationBuilder.DropTable(
                name: "session_completions");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropTable(
                name: "program_batches");

            migrationBuilder.DropTable(
                name: "attempts");

            migrationBuilder.DropTable(
                name: "program_sessions");

            migrationBuilder.DropTable(
                name: "assessments");

            migrationBuilder.DropTable(
                name: "programs");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_session_id",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_module_id",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_watch_progress_user_id_session_id",
                table: "watch_progress");

            migrationBuilder.DropCheckConstraint(
                name: "ck_watch_progress_one_target",
                table: "watch_progress");

            migrationBuilder.DropIndex(
                name: "ix_certificates_attempt_id",
                table: "certificates");

            migrationBuilder.DropIndex(
                name: "ix_certificates_program_id",
                table: "certificates");

            migrationBuilder.DropIndex(
                name: "ix_certificates_user_id_level_id",
                table: "certificates");

            migrationBuilder.DropIndex(
                name: "ix_certificates_user_id_program_id",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "watch_progress");

            migrationBuilder.DropColumn(
                name: "attempt_id",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "predicted_band",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "program_id",
                table: "certificates");

            migrationBuilder.DropColumn(
                name: "section_scores",
                table: "certificates");

            migrationBuilder.AlterColumn<Guid>(
                name: "module_id",
                table: "watch_progress",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "level_id",
                table: "certificates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id_module_id",
                table: "watch_progress",
                columns: new[] { "user_id", "module_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_user_id_level_id",
                table: "certificates",
                columns: new[] { "user_id", "level_id" },
                unique: true);
        }
    }
}
