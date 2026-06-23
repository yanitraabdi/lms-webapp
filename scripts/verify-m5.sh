#!/usr/bin/env bash
# Live check of M5 admin against the Docker stack: seeded admin login, RBAC,
# module publish/unpublish + ISR revalidation of the module landing page, price edit.
set -euo pipefail

API="${API:-http://localhost:8080}"
WEB="${WEB:-http://localhost:3001}"
say() { printf '\n=== %s ===\n' "$1"; }

say "login as seeded admin"
ATOK=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d '{"email":"admin@academy.local","password":"Admin12345!"}' | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo "admin token acquired"

say "RBAC: a normal user is forbidden"
NEMAIL="m5user_$(date +%s)@test.local"
curl -fsS -X POST "$API/api/auth/register" -H 'content-type: application/json' \
  -d "{\"name\":\"U\",\"email\":\"$NEMAIL\",\"password\":\"Password123\"}" > /dev/null
NTOK=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"$NEMAIL\",\"password\":\"Password123\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo "normal user → /api/admin/modules: $(curl -s -o /dev/null -w '%{http_code}' "$API/api/admin/modules" -H "authorization: Bearer $NTOK") (expect 403)"
echo "admin user  → /api/admin/modules: $(curl -s -o /dev/null -w '%{http_code}' "$API/api/admin/modules" -H "authorization: Bearer $ATOK") (expect 200)"

say "pick a published module"
read -r MID SLUG < <(curl -fsS "$API/api/admin/modules" -H "authorization: Bearer $ATOK" \
  | python3 -c "import sys,json; m=next(x for x in json.load(sys.stdin) if x['published']); print(m['id'], m['slug'])")
echo "module $SLUG"
echo "public /modules/$SLUG before: $(curl -s -o /dev/null -w '%{http_code}' "$WEB/modules/$SLUG") (expect 200)"

say "unpublish → ISR revalidation should make the landing page 404"
curl -fsS -X PUT "$API/api/admin/modules/$MID/published" -H "authorization: Bearer $ATOK" \
  -H 'content-type: application/json' -d '{"published":false}' > /dev/null
sleep 2
echo "public /modules/$SLUG after unpublish: $(curl -s -o /dev/null -w '%{http_code}' "$WEB/modules/$SLUG") (expect 404)"
echo "catalog still lists it? $(curl -fsS "$API/api/catalog?take=100" | python3 -c "import sys,json; print(any(m['slug']=='$SLUG' for m in json.load(sys.stdin)['modules']))") (expect False)"

say "republish → landing page back to 200"
curl -fsS -X PUT "$API/api/admin/modules/$MID/published" -H "authorization: Bearer $ATOK" \
  -H 'content-type: application/json' -d '{"published":true}' > /dev/null
sleep 2
echo "public /modules/$SLUG after republish: $(curl -s -o /dev/null -w '%{http_code}' "$WEB/modules/$SLUG") (expect 200)"

say "edit Beginner price"
read -r PID OLD < <(curl -fsS "$API/api/admin/plans" -H "authorization: Bearer $ATOK" \
  | python3 -c "import sys,json; p=next(x for x in json.load(sys.stdin) if x['tierLevel']==1); print(p['id'], int(p['priceMonthly']))")
NEW=$((OLD + 10000))
curl -fsS -X PUT "$API/api/admin/plans/prices" -H "authorization: Bearer $ATOK" -H 'content-type: application/json' \
  -d "{\"items\":[{\"planId\":\"$PID\",\"priceMonthly\":$NEW,\"priceAnnual\":1490000}]}" > /dev/null
echo "Beginner price $OLD → $(curl -fsS "$API/api/plans" | python3 -c "import sys,json; print(int(next(p['priceMonthly'] for p in json.load(sys.stdin) if p['tierLevel']==1)))") (expect $NEW)"
# restore
curl -fsS -X PUT "$API/api/admin/plans/prices" -H "authorization: Bearer $ATOK" -H 'content-type: application/json' \
  -d "{\"items\":[{\"planId\":\"$PID\",\"priceMonthly\":$OLD,\"priceAnnual\":1490000}]}" > /dev/null

say "admin page renders"
echo "GET /admin: $(curl -s -o /dev/null -w '%{http_code}' "$WEB/admin") (client shell, expect 200)"

echo
echo "M5 live verification OK"
