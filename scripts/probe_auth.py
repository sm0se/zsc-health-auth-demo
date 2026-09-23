#!/usr/bin/env python3
"""Capture auth behavior through the gateway for both health routes and device status."""
import json
from urllib.error import HTTPError
from urllib.request import Request, urlopen

BASE = "http://127.0.0.1:5080"
KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"


def request(label, path, headers=None, method="GET"):
    req = Request(BASE + path, headers=headers or {}, method=method)
    try:
        with urlopen(req, timeout=10) as response:
            body = response.read().decode("utf-8", errors="replace")
            status = response.status
    except HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        status = error.code
    excerpt = body[:240]
    if label == "mint-token":
        excerpt = '{"access_token":"<redacted>","token_type":"Bearer",...}'
    print(f"{label}: {status} {excerpt}")
    return status, body


_, token_payload = request("mint-token", "/dev/token", method="POST")
token = json.loads(token_payload)["access_token"]
health = ("/api/v1/health/zsc/status", "/api/v1/health/zls/status")

for path in health:
    request(f"valid-key {path}", path, {"Ocp-Apim-Subscription-Key": KEY})
for path in health:
    request(f"wrong-key {path}", path, {"Ocp-Apim-Subscription-Key": WRONG_KEY})
request("no-credentials /api/v1/health/zsc/status", health[0])
request("bearer-alone /api/v1/health/zsc/status", health[0], {"Authorization": f"Bearer {token}"})
request("device-key-alone /api/v1/devices/dev-0001/status", "/api/v1/devices/dev-0001/status", {"Ocp-Apim-Subscription-Key": KEY})
request("device-bearer /api/v1/devices/dev-0001/status", "/api/v1/devices/dev-0001/status", {"Authorization": f"Bearer {token}"})
request("gateway-healthz-anonymous", "/healthz")
