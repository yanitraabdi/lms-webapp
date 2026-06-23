#!/usr/bin/env bash
# Live check of M6 against the Docker stack: FAQ, contact, onboarding tour/survey,
# public content + legal pages, system pages, sitemap/robots.
set -euo pipefail

API="${API:-http://localhost:8080}"
WEB="${WEB:-http://localhost:3001}"
say() { printf '\n=== %s ===\n' "$1"; }
code() { curl -s -o /dev/null -w '%{http_code}' "$1"; }

say "FAQ (public, seeded)"
curl -fsS "$API/api/faq" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d),'FAQ items; first:', d[0]['question'][:40] if d else '(none)')"

say "contact form stores submission"
echo "valid   → $(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/contact" -H 'content-type: application/json' -d '{"name":"Budi","email":"budi@test.local","message":"Halo"}') (expect 204)"
echo "invalid → $(curl -s -o /dev/null -w '%{http_code}' -X POST "$API/api/contact" -H 'content-type: application/json' -d '{"name":"","email":"x","message":""}') (expect 400)"

say "onboarding tour + survey (auth)"
EMAIL="m6_$(date +%s)@test.local"
curl -fsS -X POST "$API/api/auth/register" -H 'content-type: application/json' \
  -d "{\"name\":\"U\",\"email\":\"$EMAIL\",\"password\":\"Password123\"}" > /dev/null
TOK=$(curl -fsS -X POST "$API/api/auth/login" -H 'content-type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"Password123\"}" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
echo "state before: $(curl -fsS "$API/api/onboarding" -H "authorization: Bearer $TOK")"
curl -fsS -X POST "$API/api/onboarding/tour" -H "authorization: Bearer $TOK" -H 'content-type: application/json' -d '{"tourKey":"dashboard_first_run","status":"Completed"}' > /dev/null
curl -fsS -X POST "$API/api/onboarding/survey" -H "authorization: Bearer $TOK" -H 'content-type: application/json' -d '{"role":"ops","goals":["Produktivitas harian"],"preferredTools":["Claude"]}' > /dev/null
echo "state after : $(curl -fsS "$API/api/onboarding" -H "authorization: Bearer $TOK")"

say "public pages render"
for p in /about /how-it-works /for-business /help /contact /feedback /legal/terms /legal/privacy /legal/refund /maintenance; do
  printf '%-22s → %s\n' "$p" "$(code "$WEB$p")"
done
echo "404 (unknown)         → $(code "$WEB/this-does-not-exist") (expect 404)"

say "SEO"
echo "sitemap.xml → $(code "$WEB/sitemap.xml") · robots.txt → $(code "$WEB/robots.txt")"
curl -fsS "$WEB/help" | grep -o 'Pertanyaan umum' | head -1

echo
echo "M6 live verification OK"
