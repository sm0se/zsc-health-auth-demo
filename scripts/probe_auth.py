#!/usr/bin/env python3
"""Probes the nine R1 authentication cases through the gateway on :5080.

Mints a bearer token from POST /dev/token, then exercises each of the nine
target cases from the task brief, printing a label, the HTTP status and a
short excerpt of the response body. Deliberately dependency-free (urllib
only) so it runs with nothing beyond the stdlib.
"""
import json
import urllib.error
import urllib.request

GATEWAY = "http://127.0.0.1:5080"
VALID_KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"
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
    except Exception as exc:  # noqa: BLE001 - report anything as a failure
        return None, f"<exception: {exc}>"


def mint_token():
    status, body = call("POST", "/dev/token")
    if status != 200:
        raise SystemExit(f"could not mint a dev token: {status} {body}")
    payload = json.loads(body)
    return payload["access_token"]


def excerpt(body, n=160):
    body = body.replace("\n", " ")
    return body[:n] + ("..." if len(body) > n else "")


def report(label, status, body):
    print(f"{label}: status={status} body={excerpt(body)}")


def main():
    token = mint_token()
    bearer = {"Authorization": f"Bearer {token}"}
    valid_key = {KEY_HEADER: VALID_KEY}
    wrong_key = {KEY_HEADER: WRONG_KEY}

    cases = [
        ("health/zsc valid-key", "GET", "/api/v1/health/zsc/status", valid_key),
        ("health/zsc wrong-key", "GET", "/api/v1/health/zsc/status", wrong_key),
        ("health/zsc no-creds", "GET", "/api/v1/health/zsc/status", None),
        ("health/zsc bearer-alone", "GET", "/api/v1/health/zsc/status", bearer),
        ("health/zls valid-key", "GET", "/api/v1/health/zls/status", valid_key),
        ("health/zls wrong-key", "GET", "/api/v1/health/zls/status", wrong_key),
        ("health/zls no-creds", "GET", "/api/v1/health/zls/status", None),
        ("health/zls bearer-alone", "GET", "/api/v1/health/zls/status", bearer),
        ("devices key-alone", "GET", "/api/v1/devices/dev-0001/status", valid_key),
        ("devices bearer", "GET", "/api/v1/devices/dev-0001/status", bearer),
        ("healthz anonymous", "GET", "/healthz", None),
    ]

    for label, method, path, headers in cases:
        status, body = call(method, path, headers)
        report(label, status, body)


if __name__ == "__main__":
    main()
