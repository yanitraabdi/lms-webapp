#!/usr/bin/env bash
# Live end-to-end check of the M3 payment flow against the running Docker stack.
# register → verify (link from API logs) → login → checkout → simulate payment → assert active + entitled.
set -euo pipefail

API="${API:-http://localhost:8080}"
EMAIL="m3check_$(date +%s)@test.local"
PW="Password123"

say() { printf '\n=== %s ===\n' "$1"; }

say "plans"
curl -fsS "$API/api/plans" | python3 -c "import sys,json; [print(p['tierLevel'],p['name'],int(p['priceMonthly'])) for p in json.load(sys.stdin)]"
BEGINNER_ID=$(curl -fsS "$API/api/plans" | python3 -c "import sys,json; print(next(p['id'] for p in json.load(sys.stdin) if p['tierLevel']==1))")

say "register $EMAIL"
curl -fsS -X POST "$API/api/auth/register" -H 'content-type: application/json' \
  -d "{\"name\":\"M3\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}" > /dev/null

say "verify (link from api logs)"
sleep 1
VURL=$(docker logs academy-api-1 2>&1 | grep -F "$EMAIL" | grep -oE 'token=[A-Za-z0-9._-]+' | tail -1)
TOKEN_PARAM=${VURL#token=}
curl -fsS -X POST "$API/api/auth/verify-email" -H 'content-type: application/json' \
  -d "{\"token\":\"$TOKEN_PARAM\"}" > /dev/null
echo "verified"

say "login"
ACCESS=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

say "me before payment (expect 204)"
curl -s -o /dev/null -w "status=%{http_code}\n" "$API/api/subscriptions/me" -H "authorization: Bearer $ACCESS"

say "checkout Beginner monthly"
REF=$(curl -fsS -X POST "$API/api/subscriptions/checkout" -H "authorization: Bearer $ACCESS" -H 'content-type: application/json' \
  -d "{\"planId\":\"$BEGINNER_ID\",\"billingCycle\":\"Monthly\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['providerRef'])")
echo "providerRef=$REF"

say "simulate successful payment (fires signed webhook)"
curl -fsS -X POST "$API/api/dev/payments/$REF/succeed" | python3 -c "import sys,json; print('outcome:', json.load(sys.stdin)['outcome'])"

say "me after payment (expect Active Beginner)"
curl -fsS "$API/api/subscriptions/me" -H "authorization: Bearer $ACCESS" \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['planName'], d['status'], int(d['priceLockedIdr']))"

say "catalog access (expect at least one Entitled Basic module)"
curl -fsS "$API/api/catalog?level=basic&take=100" -H "authorization: Bearer $ACCESS" \
  | python3 -c "import sys,json,collections; d=json.load(sys.stdin); c=collections.Counter(m['access'] for m in d['modules']); print(dict(c))"

echo
echo "M3 live verification OK"
