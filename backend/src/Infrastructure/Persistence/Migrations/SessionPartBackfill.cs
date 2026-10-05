namespace Academy.Infrastructure.Persistence.Migrations;

/// <summary>
/// Converts every pre-2026-10-05 video session into parts: its video becomes a LessonVideo part,
/// and its gating test (if any) a Test part after it; its watch progress is pointed at the lesson
/// part. Idempotent (only sessions with no parts are touched), so tests can run it again.
/// Backfilled ids are uuid v4 (gen_random_uuid): Postgres 17 has no uuidv7(), and these few
/// rows do not need index locality. Run by migration InvertaSessionParts — never edit after merge.
/// </summary>
public static class SessionPartBackfill
{
    public const string Sql = """
        -- Fail loudly rather than drop a test: one test per part, so a shared assessment cannot be backfilled.
        DO $$
        DECLARE dup uuid;
        BEGIN
            SELECT assessment_id INTO dup FROM program_sessions
            WHERE type = 'Video' AND assessment_id IS NOT NULL
            GROUP BY assessment_id HAVING count(*) > 1 LIMIT 1;
            IF dup IS NOT NULL THEN
                RAISE EXCEPTION 'session_parts backfill: assessment % is attached to more than one video session — detach the duplicates before migrating', dup;
            END IF;
        END $$;

        INSERT INTO session_parts (id, session_id, order_index, kind, title, provider_asset_id,
                                   duration_seconds, assessment_id, created_at, updated_at)
        SELECT gen_random_uuid(), s.id, 1, 'LessonVideo', s.title,
               s.provider_asset_id, s.duration_seconds, NULL, now(), now()
        FROM program_sessions s
        WHERE s.type = 'Video' AND COALESCE(s.provider_asset_id, '') <> ''
          AND NOT EXISTS (SELECT 1 FROM session_parts p WHERE p.session_id = s.id);

        -- A session with no video gets no invented lesson; its test becomes part 1, and the
        -- readiness check flags the session for an admin to complete.
        INSERT INTO session_parts (id, session_id, order_index, kind, title, provider_asset_id,
                                   duration_seconds, assessment_id, created_at, updated_at)
        SELECT gen_random_uuid(), s.id,
               CASE WHEN COALESCE(s.provider_asset_id, '') <> '' THEN 2 ELSE 1 END,
               'Test', 'Tes sesi', NULL, NULL, s.assessment_id, now(), now()
        FROM program_sessions s
        WHERE s.type = 'Video' AND s.assessment_id IS NOT NULL
          AND NOT EXISTS (SELECT 1 FROM session_parts p WHERE p.session_id = s.id AND p.kind = 'Test');

        UPDATE watch_progress w
        SET part_id = p.id
        FROM session_parts p
        WHERE w.part_id IS NULL AND w.session_id = p.session_id AND p.kind = 'LessonVideo' AND p.order_index = 1;
        """;
}
