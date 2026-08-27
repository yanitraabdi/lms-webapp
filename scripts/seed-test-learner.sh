#!/usr/bin/env bash
# Seeds one enrolled test learner so the student flow can be clicked through end to end.
#
# Deliberately NOT a C# seeder: enrollment must only ever be granted by a verified payment
# webhook (GR-2), and a seeder writing an enrollment row directly would set exactly the wrong
# precedent. This drives the real checkout, then the dev payment endpoint, which builds a signed
# webhook and feeds it through the production webhook processor — signature, idempotency and
# state machine all exercised.
#
# Requires Billing:Provider = "dev" (the default) and the stack running.
#   Usage: scripts/seed-test-learner.sh [base-url]
set -euo pipefail

BASE="${1:-http://localhost:6300}"
EMAIL="siswa@test.local"
PASSWORD="Siswa12345!"
COMPOSE="docker compose -f docker-compose.tunnel.yml"

jqv() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }

echo "1/5 registering $EMAIL"
REG=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/register" \
  -H 'content-type: application/json' \
  -d "{\"name\":\"Siswa Uji\",\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")
case "$REG" in
  200|201) ;;
  409)     echo "    (already registered — continuing)" ;;
  429)     echo "    rate limited (HTTP 429). The auth limiter is per-IP; wait a minute and retry." >&2; exit 1 ;;
  *)       echo "    register failed: HTTP $REG" >&2; exit 1 ;;
esac

echo "2/5 marking the address verified"
$COMPOSE exec -T postgres psql -U academy -d academy -q \
  -c "update users set email_verified = true where email = '$EMAIL';" >/dev/null

echo "3/5 signing in"
TOKEN=$(curl -sf -X POST "$BASE/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" | jqv '["accessToken"]')

PROGRAM=$($COMPOSE exec -T postgres psql -U academy -d academy -t \
  -c "select id from programs where slug = 'toefl-preparation';" | tr -d ' \n')

echo "4/5 checkout (grants nothing on its own)"
ENROLL=$(curl -s -X POST "$BASE/api/programs/$PROGRAM/enroll" \
  -H "Authorization: Bearer $TOKEN" -H 'content-type: application/json' -d '{}')
if grep -q '"status":409' <<<"$ENROLL"; then
  echo "    (already enrolled — nothing to pay)"
else
  REF=$(jqv '["providerRef"]' <<<"$ENROLL")
  echo "5/5 paying — signed webhook through the real processor"
  curl -sf -X POST "$BASE/api/dev/payments/$REF/succeed" >/dev/null
fi

STATUS=$($COMPOSE exec -T postgres psql -U academy -d academy -t \
  -c "select e.status from enrollments e join users u on u.id=e.user_id where u.email='$EMAIL';" | tr -d ' \n')

echo
echo "done — $EMAIL / $PASSWORD   enrollment: $STATUS"
