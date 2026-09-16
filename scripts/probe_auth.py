#!/usr/bin/env python3
"""Probes the ZSC chain through the api-gateway with the nine credential
combinations that define R1's target behaviour, mints a dev OAuth2 token from
/dev/token, then prints label / status / a short body excerpt for each call so
before/after runs can be diffed by eye.
"""
import json
import os
import urllib.error
import urllib.request

GATEWAY = os.environ.get("ZSC_GATEWAY", "http://127.0.0.1:5080")
GOOD_KEY = os.environ.get("ZSC_SUBSCRIPTION_KEY", "zsc-demo-subscription-key-001")
BAD_KEY = "wrong-key-000"
KEY_HEADER = "Ocp-Apim-Subscription-Key"


def call(method, path, headers=None):
    url = f"{GATEWAY}{path}"
    req = urllib.request.Request(url, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            return resp.status, body
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        return exc.code, body
    except urllib.error.URLError as exc:
        return None, str(exc)


def mint_token():
    status, body = call("POST", "/dev/token")
    if status != 200:
        raise SystemExit(f"could not mint a dev token: {status} {body}")
    payload = json.loads(body)
    return payload["access_token"]


def excerpt(body, n=140):
    body = body.replace("\n", " ")
    return body[:n] + ("..." if len(body) > n else "")


def report(label, status, body):
    print(f"{label:55s} -> {status}  {excerpt(body)}")


def main():
    token = mint_token()
    bearer = {"Authorization": f"Bearer {token}"}
    good_key = {KEY_HEADER: GOOD_KEY}
    bad_key = {KEY_HEADER: BAD_KEY}

    cases = [
        ("health/zsc  key=valid",      "GET", "/api/v1/health/zsc/status", good_key),
        ("health/zsc  key=wrong",      "GET", "/api/v1/health/zsc/status", bad_key),
        ("health/zsc  no-credentials", "GET", "/api/v1/health/zsc/status", {}),
        ("health/zsc  bearer-alone",   "GET", "/api/v1/health/zsc/status", bearer),
        ("health/zls  key=valid",      "GET", "/api/v1/health/zls/status", good_key),
        ("health/zls  key=wrong",      "GET", "/api/v1/health/zls/status", bad_key),
        ("health/zls  no-credentials", "GET", "/api/v1/health/zls/status", {}),
        ("health/zls  bearer-alone",   "GET", "/api/v1/health/zls/status", bearer),
        ("device dev-0001  key-alone", "GET", "/api/v1/devices/dev-0001/status", good_key),
        ("device dev-0001  bearer",    "GET", "/api/v1/devices/dev-0001/status", bearer),
    ]

    print(f"gateway: {GATEWAY}")
    print(f"good key: {GOOD_KEY}   wrong key: {BAD_KEY}")
    print()
    for label, method, path, headers in cases:
        status, body = call(method, path, headers)
        report(label, status, body)


if __name__ == "__main__":
    main()
