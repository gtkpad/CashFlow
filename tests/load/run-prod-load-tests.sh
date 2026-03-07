#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
mkdir -p "$RESULTS_DIR"

# ── Discover gateway URL ──────────────────────────────────────────────
echo "Discovering gateway URL..."
GATEWAY_URL="${BASE_URL:-}"
if [[ -z "$GATEWAY_URL" ]]; then
  GATEWAY_URL="$(azd env get-value GATEWAY_URL 2>/dev/null || true)"
fi
if [[ -z "$GATEWAY_URL" ]]; then
  echo "Falling back to az containerapp show..."
  GATEWAY_URL="https://$(az containerapp show \
    --name gateway \
    --resource-group "$(azd env get-value AZURE_RESOURCE_GROUP)" \
    --query 'properties.configuration.ingress.fqdn' -o tsv)"
fi
if [[ -z "$GATEWAY_URL" ]]; then
  echo "ERROR: Could not determine gateway URL. Set BASE_URL or configure azd environment."
  exit 1
fi
echo "Gateway URL: $GATEWAY_URL"

# ── Register test user and obtain JWT token ───────────────────────────
TEST_EMAIL="loadtest-$(date +%s)@test.com"
TEST_PASSWORD="LoadTest123!"

echo "Registering test user: $TEST_EMAIL"
curl -sf -X POST "$GATEWAY_URL/api/identity/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}" > /dev/null 2>&1 || true

echo "Logging in..."
LOGIN_RESPONSE=$(curl -sf -X POST "$GATEWAY_URL/api/identity/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}")

TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)
if [[ -z "$TOKEN" ]]; then
  echo "ERROR: Failed to obtain JWT token."
  echo "Response: $LOGIN_RESPONSE"
  exit 1
fi
echo "JWT token obtained."

# ── Run NFR-2: Consolidation throughput ───────────────────────────────
echo ""
echo "Running NFR-2: Consolidation throughput (50 req/s, 2min)..."
k6 run \
  -e BASE_URL="$GATEWAY_URL" \
  -e AUTH_URL="$GATEWAY_URL" \
  -e TEST_EMAIL="$TEST_EMAIL" \
  -e TEST_PASSWORD="$TEST_PASSWORD" \
  "$SCRIPT_DIR/nfr2-consolidation-throughput.js" || true

# ── Run NFR-4: Transaction ingestion ──────────────────────────────────
echo ""
echo "Running NFR-4: Transaction ingestion (50 req/s, 2min)..."
k6 run \
  -e BASE_URL="$GATEWAY_URL" \
  -e AUTH_URL="$GATEWAY_URL" \
  -e TEST_EMAIL="$TEST_EMAIL" \
  -e TEST_PASSWORD="$TEST_PASSWORD" \
  "$SCRIPT_DIR/nfr4-transaction-ingestion.js" || true

# ── Consolidated report ───────────────────────────────────────────────
echo ""
echo ""

NFR2_FILE="$RESULTS_DIR/nfr2-result.json"
NFR4_FILE="$RESULTS_DIR/nfr4-result.json"

# Extract a value from a JSON file using dot-separated keys
read_json() {
  local file="$1"
  shift
  python3 -c "
import json, sys
d = json.load(open(sys.argv[1]))
keys = sys.argv[2:]
for k in keys:
    d = d[k]
print(d)
" "$file" "$@" 2>/dev/null || echo "N/A"
}

format_icon() {
  if [[ "$1" == "True" || "$1" == "true" ]]; then
    echo "✅"
  else
    echo "❌"
  fi
}

if [[ -f "$NFR2_FILE" && -f "$NFR4_FILE" ]]; then
  NFR2_P95=$(read_json "$NFR2_FILE" thresholds p95_ms actual)
  NFR2_P95_PASS=$(read_json "$NFR2_FILE" thresholds p95_ms pass)
  NFR2_ERR=$(read_json "$NFR2_FILE" thresholds error_rate actual)
  NFR2_ERR_PASS=$(read_json "$NFR2_FILE" thresholds error_rate pass)
  NFR2_OVERALL=$(read_json "$NFR2_FILE" overall_pass)

  NFR4_P95=$(read_json "$NFR4_FILE" thresholds p95_ms actual)
  NFR4_P95_PASS=$(read_json "$NFR4_FILE" thresholds p95_ms pass)
  NFR4_ERR=$(read_json "$NFR4_FILE" thresholds error_rate actual)
  NFR4_ERR_PASS=$(read_json "$NFR4_FILE" thresholds error_rate pass)
  NFR4_OVERALL=$(read_json "$NFR4_FILE" overall_pass)

  NFR2_P95_ICON=$(format_icon "$NFR2_P95_PASS")
  NFR2_ERR_ICON=$(format_icon "$NFR2_ERR_PASS")
  NFR4_P95_ICON=$(format_icon "$NFR4_P95_PASS")
  NFR4_ERR_ICON=$(format_icon "$NFR4_ERR_PASS")

  NFR2_ERR_PCT=$(python3 -c "print(f'{float($NFR2_ERR)*100:.2f}%')" 2>/dev/null || echo "N/A")
  NFR4_ERR_PCT=$(python3 -c "print(f'{float($NFR4_ERR)*100:.2f}%')" 2>/dev/null || echo "N/A")

  printf '╔══════════════════════════════════════════════════════════╗\n'
  printf '║              NFR Load Test Report                       ║\n'
  printf '╠═══════╦════════════════════════╦════════╦═══════════════╣\n'
  printf '║ NFR   ║ Metric                 ║ Target ║ Actual        ║\n'
  printf '╠═══════╬════════════════════════╬════════╬═══════════════╣\n'
  printf '║ NFR-2 ║ Consolidation p95      ║ <200ms ║ %6sms %s   ║\n' "$NFR2_P95" "$NFR2_P95_ICON"
  printf '║ NFR-2 ║ Error rate             ║ <1%%    ║ %6s  %s   ║\n' "$NFR2_ERR_PCT" "$NFR2_ERR_ICON"
  printf '║ NFR-4 ║ Transaction p95        ║ <500ms ║ %6sms %s   ║\n' "$NFR4_P95" "$NFR4_P95_ICON"
  printf '║ NFR-4 ║ Error rate             ║ <1%%    ║ %6s  %s   ║\n' "$NFR4_ERR_PCT" "$NFR4_ERR_ICON"
  printf '╚═══════╩════════════════════════╩════════╩═══════════════╝\n'
  echo ""

  NFR2_STATUS=$( [[ "$(format_icon "$NFR2_OVERALL")" == "✅" ]] && echo "PASS ✅" || echo "FAIL ❌" )
  NFR4_STATUS=$( [[ "$(format_icon "$NFR4_OVERALL")" == "✅" ]] && echo "PASS ✅" || echo "FAIL ❌" )
  echo "Overall: NFR-2 $NFR2_STATUS | NFR-4 $NFR4_STATUS"
  echo ""
  echo "Detailed results:"
  echo "  $NFR2_FILE"
  echo "  $NFR4_FILE"
else
  echo "WARNING: Result files not found. Check k6 output above for errors."
  [[ ! -f "$NFR2_FILE" ]] && echo "  Missing: $NFR2_FILE"
  [[ ! -f "$NFR4_FILE" ]] && echo "  Missing: $NFR4_FILE"
fi
