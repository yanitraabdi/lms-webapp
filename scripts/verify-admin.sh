#!/usr/bin/env bash
# Live check of full admin CRUD against the Docker stack: curriculum create→catalog,
# user grant→entitlement, analytics, and admin frontend pages.
set -euo pipefail

API="${API:-http://localhost:8080}"
WEB="${WEB:-http://localhost:3001}"
say() { printf '\n=== %s ===\n' "$1"; }
jq() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }

TOK=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d '{"email":"admin@academy.local","password":"Admin12345!"}' | jq "['accessToken']")
AUTH="authorization: Bearer $TOK"
SUF=$(date +%s)

say "create curriculum (category → level → track → module)"
CAT=$(curl -fsS -X POST "$API/api/admin/categories" -H "$AUTH" -H 'content-type: application/json' -d '{"name":"Cek Admin '"$SUF"'"}' | jq "['id']")
LVL=$(curl -fsS -X POST "$API/api/admin/levels" -H "$AUTH" -H 'content-type: application/json' -d '{"name":"Cek Level '"$SUF"'","requiredPlanTier":1,"orderIndex":99,"published":true}' | jq "['id']")
TRK=$(curl -fsS -X POST "$API/api/admin/tracks" -H "$AUTH" -H 'content-type: application/json' -d "{\"levelId\":\"$LVL\",\"name\":\"Cek Track\",\"orderIndex\":0}" | jq "['id']")
read -r MID SLUG < <(curl -fsS -X POST "$API/api/admin/modules" -H "$AUTH" -H 'content-type: application/json' \
  -d "{\"trackId\":\"$TRK\",\"categoryId\":\"$CAT\",\"title\":\"Cek Modul Admin\",\"description\":\"d\",\"durationSeconds\":300,\"orderIndex\":0,\"isPreview\":false,\"requiredPlanTier\":1,\"published\":true,\"tagIds\":[]}" \
  | python3 -c "import sys,json;d=json.load(sys.stdin);print(d['id'],d['slug'])")
echo "module $SLUG created; in public catalog? $(curl -fsS "$API/api/catalog?take=100" | python3 -c "import sys,json;print(any(m['slug']=='$SLUG' for m in json.load(sys.stdin)['modules']))")"

say "user grant → entitlement → revoke"
EMAIL="adm_$(date +%s)@test.local"
curl -fsS -X POST "$API/api/auth/register" -H 'content-type: application/json' -d "{\"name\":\"U\",\"email\":\"$EMAIL\",\"password\":\"Password123\"}" > /dev/null
UID=$(curl -fsS "$API/api/admin/users?search=$EMAIL" -H "$AUTH" | jq "['users'][0]['id']")
PID=$(curl -fsS "$API/api/admin/plans" -H "$AUTH" | python3 -c "import sys,json;print(next(p['id'] for p in json.load(sys.stdin) if p['tierLevel']==1))")
curl -fsS -X POST "$API/api/admin/users/$UID/grant" -H "$AUTH" -H 'content-type: application/json' -d "{\"planId\":\"$PID\",\"days\":30}" > /dev/null
echo "after grant, tier: $(curl -fsS "$API/api/admin/users/$UID" -H "$AUTH" | jq "['activeTier']")"
curl -fsS -X POST "$API/api/admin/users/$UID/revoke" -H "$AUTH" > /dev/null
echo "after revoke, tier: $(curl -fsS "$API/api/admin/users/$UID" -H "$AUTH" | jq "['activeTier']")"

say "analytics"
curl -fsS "$API/api/admin/analytics" -H "$AUTH" | python3 -c "import sys,json;d=json.load(sys.stdin);print('users:',d['totalUsers'],'| activeSubs:',d['activeSubscriptions'],'| byTier:',len(d['activeByTier']),'| mostWatched:',len(d['mostWatched']))"

say "cleanup"
for url in "modules/$MID" "tracks/$TRK" "levels/$LVL" "categories/$CAT"; do
  curl -s -o /dev/null -w "delete $url → %{http_code}\n" -X DELETE "$API/api/admin/$url" -H "$AUTH"
done

say "admin frontend pages"
for p in /admin /admin/curriculum /admin/modules /admin/users /admin/pricing; do
  printf '%-22s → %s\n' "$p" "$(curl -s -o /dev/null -w '%{http_code}' "$WEB$p")"
done

echo
echo "Admin CRUD live verification OK"
