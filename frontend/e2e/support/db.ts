import { execFileSync } from "node:child_process";

const lit = (s: string) => `'${s.replace(/'/g, "''")}'`;

/** Runs SQL in the e2e Postgres (container's own credentials); returns trimmed `-t -A` output. */
export function psql(sql: string): string {
  return execFileSync(
    "docker",
    ["compose", "-p", "inverta-e2e", "--env-file", "../e2e.env", "-f", "../docker-compose.yml", "-f", "../docker-compose.e2e.yml",
      "exec", "-T", "postgres", "sh", "-c", 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -t -A -c "$0"', sql],
    { encoding: "utf8" },
  ).trim();
}

export function verifyEmail(email: string): void {
  psql(`UPDATE users SET email_verified = true WHERE email = ${lit(email)}`);
}

export function userId(email: string): string {
  return psql(`SELECT id FROM users WHERE email = ${lit(email)}`);
}

/** question id -> correct choice index (questions.correct is a JSON int array; element 0). */
export function correctAnswers(assessmentId: string): Record<string, number> {
  const out = psql(
    `SELECT q.id || '|' || (q.correct::jsonb ->> 0) FROM assessment_questions aq JOIN questions q ON q.id = aq.question_id WHERE aq.assessment_id = ${lit(assessmentId)}`,
  );
  return Object.fromEntries(out.split("\n").filter(Boolean).map((l) => { const [id, i] = l.split("|"); return [id, Number(i)]; }));
}
