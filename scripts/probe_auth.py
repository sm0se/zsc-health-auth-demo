#!/usr/bin/env python3
"""Probes the ZSC gateway with the nine credential/route combinations that
define Requirement R1's target behaviour, printing label / status / body
excerpt for each. Run before and after the change to compare (results/*-probe.txt).
"""
import json
import urllib.error
import urllib.request

GATEWAY = "http://127.0.0.1:5080"
VALID_KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"
KEY_HEADER = "Ocp-Apim-Subscription-Key"


def mint_token() -> str:
    req = urllib.request.Request(f"{GATEWAY}/dev/token", method="POST")
    with urllib.request.urlopen(req, timeout=10) as resp:
        payload = json.loads(resp.read().decode())
    return payload["access_token"]


def call(label: str, path: str, headers: dict) -> None:
    req = urllib.request.Request(f"{GATEWAY}{path}", headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            status = resp.status
            body = resp.read().decode(errors="replace")
    except urllib.error.HTTPError as exc:
        status = exc.code
        body = exc.read().decode(errors="replace")
    except Exception as exc:  # noqa: BLE001
        status = "ERROR"
        body = str(exc)
    excerpt = body[:200].replace("\n", " ")
    print(f"{label}: status={status} body={excerpt}")


def main() -> None:
    token = mint_token()
    bearer = {"Authorization": f"Bearer {token}"}
    valid_key = {KEY_HEADER: VALID_KEY}
    wrong_key = {KEY_HEADER: WRONG_KEY}

    cases = [
        ("zsc-status valid-key", "/api/v1/health/zsc/status", valid_key),
        ("zsc-status wrong-key", "/api/v1/health/zsc/status", wrong_key),
        ("zsc-status no-creds", "/api/v1/health/zsc/status", {}),
        ("zsc-status bearer-alone", "/api/v1/health/zsc/status", bearer),
        ("zls-status valid-key", "/api/v1/health/zls/status", valid_key),
        ("zls-status wrong-key", "/api/v1/health/zls/status", wrong_key),
        ("zls-status no-creds", "/api/v1/health/zls/status", {}),
        ("zls-status bearer-alone", "/api/v1/health/zls/status", bearer),
        ("device-status key-alone", "/api/v1/devices/dev-0001/status", valid_key),
        ("device-status bearer", "/api/v1/devices/dev-0001/status", bearer),
    ]

    print(f"# probe against {GATEWAY}")
    for label, path, headers in cases:
        call(label, path, headers)

    call("healthz", "/healthz", {})


if __name__ == "__main__":
    main()
