#!/usr/bin/env bash
# Probes authentication behavior through the chain.
# Requires all five services running. Start with scripts/run-all.sh.
set -uo pipefail
cd "$(dirname "$0")/.."

GATEWAY=${ZSC_GATEWAY:-http://127.0.0.1:5080}
SUBSCRIPTION_KEY=${ZSC_SUBSCRIPTION_KEY:-zsc-demo-subscription-key-001}

echo "Minting bearer token from POST $GATEWAY/dev/token"
TOKEN=$(curl -fsS -X POST "$GATEWAY/dev/token" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')
if [ -z "$TOKEN" ]; then
  echo "ERROR: could not mint token"
  exit 1
fi
echo "Token: ${TOKEN:0:30}..."
echo

# Health Status ZSC API tests
echo "=== GET /api/v1/health/zsc/status ==="
echo "1. Valid subscription key:"
curl -s -w "  Status: %{http_code}\n" -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" "$GATEWAY/api/v1/health/zsc/status" | head -3
echo

echo "2. Wrong subscription key:"
curl -s -w "  Status: %{http_code}\n" -H "Ocp-Apim-Subscription-Key: wrong-key-000" "$GATEWAY/api/v1/health/zsc/status" | head -3
echo

echo "3. No credentials:"
curl -s -w "  Status: %{http_code}\n" "$GATEWAY/api/v1/health/zsc/status" | head -3
echo

echo "4. Bearer token only (OAuth2):"
curl -s -w "  Status: %{http_code}\n" -H "Authorization: Bearer $TOKEN" "$GATEWAY/api/v1/health/zsc/status" | head -3
echo

# Device API tests
echo "=== GET /api/v1/devices/dev-0001/status ==="
echo "5. Subscription key only:"
curl -s -w "  Status: %{http_code}\n" -H "Ocp-Apim-Subscription-Key: $SUBSCRIPTION_KEY" "$GATEWAY/api/v1/devices/dev-0001/status" | head -3
echo

echo "6. Bearer token only (OAuth2):"
curl -s -w "  Status: %{http_code}\n" -H "Authorization: Bearer $TOKEN" "$GATEWAY/api/v1/devices/dev-0001/status" | head -3
echo
