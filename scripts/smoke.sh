#!/usr/bin/env bash
# Exercises the full chain against running processes and asserts the status code
# of each call. Run scripts/run-all.sh first.
set -uo pipefail
cd "$(dirname "$0")/.."

GATEWAY=${ZSC_GATEWAY:-http://127.0.0.1:5080}
SUBSCRIPTION_KEY=${ZSC_SUBSCRIPTION_KEY:-zsc-demo-subscription-key-001}
failures=0

check() {
  local label=$1 expected=$2; shift 2
  local actual; actual=$(curl -s -o /tmp/zsc-smoke-body -w '%{http_code}' "$@")
  if [ "$actual" = "$expected" ]; then
    echo "  ok    $label -> $actual"
  else
    echo "  FAIL  $label -> $actual (expected $expected)"
    sed -n '1,3p' /tmp/zsc-smoke-body | sed 's/^/        /'
    failures=$((failures + 1))
  fi
}

TOKEN=$(curl -fsS -X POST "$GATEWAY/dev/token" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
[ -n "$TOKEN" ] || { echo "could not mint a dev token from $GATEWAY/dev/token"; exit 1; }

echo "liveness"
check "gateway /healthz" 200 "$GATEWAY/healthz"

echo "OAuth2 endpoints"
check "device status, no credentials" 401 "$GATEWAY/api/v1/devices/dev-0001/status"
check "device status, bearer" 200 -H "Authorization: Bearer $TOKEN" "$GATEWAY/api/v1/devices/dev-0001/status"

echo "health status with subscription key (R1 requirement)"
check "health zsc, subscription key" 200 -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" "$GATEWAY/api/v1/health/zsc/status"
check "health zls, subscription key" 200 -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" "$GATEWAY/api/v1/health/zls/status"

echo "health status - no longer accepts OAuth2"
check "health zsc, no credentials" 401 "$GATEWAY/api/v1/health/zsc/status"
check "health zsc, bearer (not accepted)" 401 -H "Authorization: Bearer $TOKEN" "$GATEWAY/api/v1/health/zsc/status"
check "health zls, bearer (not accepted)" 401 -H "Authorization: Bearer $TOKEN" "$GATEWAY/api/v1/health/zls/status"
check "health status, subscription key rejected for other APIs" 401 -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" "$GATEWAY/api/v1/devices/dev-0001/status"

echo
if [ "$failures" -eq 0 ]; then
  echo "smoke: all checks passed"
else
  echo "smoke: $failures check(s) failed"
fi
exit "$failures"
