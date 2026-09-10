#!/bin/bash
# Probe authentication across the ZSC platform.
# Mints a dev token, then tests various credential combinations.

set -e

GATEWAY="http://127.0.0.1:5080"
KEY="zsc-demo-subscription-key-001"
WRONG_KEY="wrong-key-000"

echo "=== Getting dev token ==="
RESPONSE=$(curl -s -X POST "$GATEWAY/dev/token")
TOKEN=$(echo "$RESPONSE" | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)
echo "Token: $TOKEN"
echo ""

echo "=== Test 1: GET /api/v1/health/zsc/status with valid subscription key ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/health/zsc/status" -H "Ocp-Apim-Subscription-Key: $KEY")
echo "Status: $STATUS"
if [ "$STATUS" = "200" ]; then
  curl -s "$GATEWAY/api/v1/health/zsc/status" -H "Ocp-Apim-Subscription-Key: $KEY" | head -c 200
fi
echo ""
echo ""

echo "=== Test 2: GET /api/v1/health/zls/status with valid subscription key ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/health/zls/status" -H "Ocp-Apim-Subscription-Key: $KEY")
echo "Status: $STATUS"
if [ "$STATUS" = "200" ]; then
  curl -s "$GATEWAY/api/v1/health/zls/status" -H "Ocp-Apim-Subscription-Key: $KEY" | head -c 200
fi
echo ""
echo ""

echo "=== Test 3: GET /api/v1/health/zsc/status with wrong subscription key ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/health/zsc/status" -H "Ocp-Apim-Subscription-Key: $WRONG_KEY")
echo "Status: $STATUS"
echo ""

echo "=== Test 4: GET /api/v1/health/zsc/status with no credentials ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/health/zsc/status")
echo "Status: $STATUS"
echo ""

echo "=== Test 5: GET /api/v1/health/zsc/status with bearer token only ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/health/zsc/status" -H "Authorization: Bearer $TOKEN")
echo "Status: $STATUS"
echo ""

echo "=== Test 6: GET /api/v1/devices/dev-0001/status with bearer token ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/devices/dev-0001/status" -H "Authorization: Bearer $TOKEN")
echo "Status: $STATUS"
if [ "$STATUS" = "200" ]; then
  curl -s "$GATEWAY/api/v1/devices/dev-0001/status" -H "Authorization: Bearer $TOKEN" | head -c 200
fi
echo ""
echo ""

echo "=== Test 7: GET /api/v1/devices/dev-0001/status with subscription key ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/devices/dev-0001/status" -H "Ocp-Apim-Subscription-Key: $KEY")
echo "Status: $STATUS"
echo ""

echo "=== Test 8: GET /healthz (no auth required) ==="
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/healthz")
echo "Status: $STATUS"
echo ""
