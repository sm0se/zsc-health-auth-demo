#!/usr/bin/env python3
"""Probes the running ZSC chain through the gateway (127.0.0.1:5080) with the
nine credential combinations Requirement R1 cares about, and prints label,
status code and a body excerpt for each. urllib only - no third-party deps.
"""
import json
import urllib.error
import urllib.request

GATEWAY = "http://127.0.0.1:5080"
VALID_KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"
KEY_HEADER = "Ocp-Apim-Subscription-Key"


def mint_token() -> str:
    req = urllib.request.Request(f"{GATEWAY}/dev/token", method="POST", data=b"")
    with urllib.request.urlopen(req, timeout=10) as resp:
        payload = json.loads(resp.read().decode())
    return payload["access_token"]


def call(path: str, headers: dict) -> tuple[int, str]:
    req = urllib.request.Request(f"{GATEWAY}{path}", headers=headers, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            body = resp.read().decode(errors="replace")
            return resp.status, body
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")
        return e.code, body


def excerpt(body: str, n: int = 160) -> str:
    body = body.replace("\n", " ")
    return body[:n] + ("..." if len(body) > n else "")


def report(label: str, path: str, headers: dict):
    status, body = call(path, headers)
    print(f"{label:55s} -> {status:3d}  {excerpt(body)}")


def main():
    print("== minting a dev bearer token ==")
    try:
        token = mint_token()
        print(f"token minted ({len(token)} chars)\n")
    except Exception as ex:  # noqa: BLE001
        print(f"could not mint token: {ex}")
        token = None

    bearer = {"Authorization": f"Bearer {token}"} if token else {}

    print("== ZSC / ZLS health status ==")
    for platform in ("zsc", "zls"):
        path = f"/api/v1/health/{platform}/status"
        report(f"{platform} status, valid subscription key", path, {KEY_HEADER: VALID_KEY})
        report(f"{platform} status, wrong subscription key", path, {KEY_HEADER: WRONG_KEY})
        report(f"{platform} status, no credentials", path, {})
        report(f"{platform} status, bearer alone", path, bearer)

    print("\n== other ZSC API (device status) ==")
    device_path = "/api/v1/devices/dev-0001/status"
    report("device status, subscription key alone", device_path, {KEY_HEADER: VALID_KEY})
    report("device status, bearer", device_path, bearer)


if __name__ == "__main__":
    main()
