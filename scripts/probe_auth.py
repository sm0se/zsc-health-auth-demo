#!/usr/bin/env python3
"""Probes the R1 authentication contract through the gateway on :5080.

Mints a dev bearer token (POST /dev/token) and performs the nine credential
cases of Requirement #1. Prints one line per case: label, HTTP status and a
body excerpt, so before/after runs can be diffed by eye and by machine.

Stdlib only (urllib) so it runs with nothing installed.
"""

import json
import sys
import urllib.error
import urllib.request

GATEWAY = "http://127.0.0.1:5080"
VALID_KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"


def http(method, path, headers=None):
    request = urllib.request.Request(GATEWAY + path, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            return response.status, response.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode("utf-8", "replace")


def mint_token():
    status, body = http("POST", "/dev/token")
    if status != 200:
        print(f"dev-token status={status} body={body!r}")
        sys.exit(f"could not mint a dev token ({status}); is Zsc:EnableDevTokenEndpoint set on the gateway?")
    return json.loads(body)["access_token"]


def probe(label, path, headers):
    status, body = http("GET", path, headers)
    excerpt = " ".join(body.split())[:90]
    print(f"{label} | GET {path} | status={status} | body={excerpt}")


def main():
    bearer = mint_token()

    probe("P1 health-zsc valid-key ", "/api/v1/health/zsc/status", {"Ocp-Apim-Subscription-Key": VALID_KEY})
    probe("P2 health-zls valid-key ", "/api/v1/health/zls/status", {"Ocp-Apim-Subscription-Key": VALID_KEY})
    probe("P3 health-zsc wrong-key  ", "/api/v1/health/zsc/status", {"Ocp-Apim-Subscription-Key": WRONG_KEY})
    probe("P4 health-zls wrong-key  ", "/api/v1/health/zls/status", {"Ocp-Apim-Subscription-Key": WRONG_KEY})
    probe("P5 health-zsc no-creds   ", "/api/v1/health/zsc/status", {})
    probe("P6 health-zls no-creds   ", "/api/v1/health/zls/status", {})
    probe("P7 health-zsc bearer     ", "/api/v1/health/zsc/status", {"Authorization": f"Bearer {bearer}"})
    probe("P8 device key-only       ", "/api/v1/devices/dev-0001/status", {"Ocp-Apim-Subscription-Key": VALID_KEY})
    probe("P9 device bearer         ", "/api/v1/devices/dev-0001/status", {"Authorization": f"Bearer {bearer}"})


if __name__ == "__main__":
    main()
