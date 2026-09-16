#!/usr/bin/env python3
"""Probes the nine auth cases through the gateway, printing label/status/body excerpt.

Uses only the standard library so it needs no extra install. Mints a bearer
token from the gateway's dev-token endpoint, then exercises the target
behaviour for R1: subscription key alone on health routes, bearer alone on
health routes, and both on the device API.
"""
import json
import urllib.error
import urllib.request

GATEWAY = "http://127.0.0.1:5080"
VALID_KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"


def mint_token():
    req = urllib.request.Request(f"{GATEWAY}/dev/token", method="POST", data=b"")
    with urllib.request.urlopen(req, timeout=10) as resp:
        payload = json.loads(resp.read().decode())
        return payload["access_token"]


def call(path, headers):
    req = urllib.request.Request(f"{GATEWAY}{path}", headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            return resp.status, resp.read().decode(errors="replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")


def report(label, path, headers):
    status, body = call(path, headers)
    excerpt = body[:200].replace("\n", " ")
    print(f"{label}: GET {path} -> {status}")
    print(f"    body: {excerpt}")


def main():
    token = mint_token()
    print(f"minted bearer token (len={len(token)})")
    print()

    bearer = {"Authorization": f"Bearer {token}"}
    valid_key_hdr = {"Ocp-Apim-Subscription-Key": VALID_KEY}
    wrong_key_hdr = {"Ocp-Apim-Subscription-Key": WRONG_KEY}

    for path in ("/api/v1/health/zsc/status", "/api/v1/health/zls/status"):
        report(f"[{path}] valid key -> expect 200", path, valid_key_hdr)
        report(f"[{path}] wrong key -> expect 401", path, wrong_key_hdr)
        report(f"[{path}] no credentials -> expect 401", path, {})
        report(f"[{path}] bearer alone -> expect 200", path, bearer)

    device_path = "/api/v1/devices/dev-0001/status"
    report(f"[{device_path}] key alone -> expect 401", device_path, valid_key_hdr)
    report(f"[{device_path}] bearer -> expect 200", device_path, bearer)


if __name__ == "__main__":
    main()
