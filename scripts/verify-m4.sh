#!/usr/bin/env bash
# Live end-to-end check of the M4 flow against the running Docker stack:
# subscribe → signed playback → complete all Basic modules → certificate issued →
# public verify → PDF renders (confirms PDFsharp works inside the Linux container).
set -euo pipefail

API="${API:-http://localhost:8080}"
EMAIL="m4check_$(date +%s)@test.local"
PW="Password123"
say() { printf '\n=== %s ===\n' "$1"; }

BEGINNER_ID=$(curl -fsS "$API/api/plans" | python3 -c "import sys,json; print(next(p['id'] for p in json.load(sys.stdin) if p['tierLevel']==1))")

say "register + verify + login"
curl -fsS -X POST "$API/api/auth/register" -H 'content-type: application/json' \
  -d "{\"name\":\"Budi Santoso\",\"email\":\"$EMAIL\",\"password\":\"$PW\"}" > /dev/null
sleep 1
VTOK=$(docker logs academy-api-1 2>&1 | grep -F "$EMAIL" | grep -oE 'token=[A-Za-z0-9._-]+' | tail -1)
curl -fsS -X POST "$API/api/auth/verify-email" -H 'content-type: application/json' -d "{\"token\":\"${VTOK#token=}\"}" > /dev/null
ACCESS=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PW\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
AUTH="authorization: Bearer $ACCESS"

say "subscribe Beginner"
REF=$(curl -fsS -X POST "$API/api/subscriptions/checkout" -H "$AUTH" -H 'content-type: application/json' \
  -d "{\"planId\":\"$BEGINNER_ID\",\"billingCycle\":\"Monthly\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['providerRef'])")
curl -fsS -X POST "$API/api/dev/payments/$REF/succeed" > /dev/null
echo "subscribed"

say "signed playback on a Basic module"
MID=$(curl -fsS "$API/api/catalog?level=basic&take=1" | python3 -c "import sys,json; print(json.load(sys.stdin)['modules'][0]['id'])")
curl -fsS -X POST "$API/api/modules/$MID/playback" -H "$AUTH" \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print('url ok:', d['url'][:48]+'...'); print('expiresAt:', d['expiresAt'])"

say "complete all Basic modules"
IDS=$(curl -fsS "$API/api/catalog?level=basic&take=100" | python3 -c "import sys,json; print(' '.join(m['id'] for m in json.load(sys.stdin)['modules']))")
for id in $IDS; do
  curl -fsS -X PUT "$API/api/modules/$id/progress" -H "$AUTH" -H 'content-type: application/json' \
    -d '{"positionSeconds":600,"percent":100}' > /dev/null
done
echo "completed $(echo $IDS | wc -w | tr -d ' ') modules"

say "certificate issued"
read -r CID CODE < <(curl -fsS "$API/api/me/certificates" -H "$AUTH" | python3 -c "import sys,json; c=json.load(sys.stdin)[0]; print(c['id'], c['verificationCode'])")
echo "cert $CODE (level Basic)"

say "public verification (no auth)"
curl -fsS "$API/api/certificates/verify/$CODE" | python3 -c "import sys,json; d=json.load(sys.stdin); print('valid:', d['valid'], '| name:', d['recipientName'], '| level:', d['levelName'])"

say "certificate PDF renders in the Linux container"
curl -fsS "$API/api/certificates/$CID/pdf" -H "$AUTH" -o /tmp/cert.pdf
printf 'magic: %s | size: %s bytes\n' "$(head -c 4 /tmp/cert.pdf)" "$(wc -c < /tmp/cert.pdf | tr -d ' ')"

say "dashboard"
curl -fsS "$API/api/me/dashboard" -H "$AUTH" | python3 -c "
import sys,json; d=json.load(sys.stdin)
b=next(l for l in d['levels'] if l['slug']=='basic')
print('overall:', d['overall']['percent'], '% | Basic certified:', b['certified'], '| Basic shows:', b['percent'], '%')
"

echo
echo "M4 live verification OK"
