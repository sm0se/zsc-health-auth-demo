#!/usr/bin/env python3
"""
Probe authentication scenarios across the ZSC platform.
Mints a dev token then performs nine test calls.
Uses only urllib, no bash substitution.
"""
import sys
import json
import urllib.request
import urllib.error
from urllib.parse import urljoin

GATEWAY = "http://127.0.0.1:5080"
KEY = "zsc-demo-subscription-key-001"
WRONG_KEY = "wrong-key-000"

def mint_token():
    """Get a bearer token from POST /dev/token"""
    url = urljoin(GATEWAY, "/dev/token")
    try:
        with urllib.request.urlopen(url, data=b"") as resp:
            data = json.loads(resp.read().decode("utf-8"))
            return data.get("access_token")
    except Exception as e:
        print(f"ERROR minting token: {e}", file=sys.stderr)
        return None

def probe(label, path, key=None, bearer=None):
    """Make a single probe request, print label, status code, and body excerpt"""
    print(f"\n=== {label} ===")
    url = urljoin(GATEWAY, path)
    
    req = urllib.request.Request(url, method="GET")
    if key:
        req.add_header("Ocp-Apim-Subscription-Key", key)
    if bearer:
        req.add_header("Authorization", f"Bearer {bearer}")
    
    try:
        with urllib.request.urlopen(req) as resp:
            status = resp.status
            body = resp.read().decode("utf-8")
            print(f"Status: {status}")
            # Print first 200 chars of body
            if body:
                excerpt = body[:200]
                print(f"Body: {excerpt}")
            else:
                print("Body: (empty)")
    except urllib.error.HTTPError as e:
        status = e.code
        try:
            body = e.read().decode("utf-8")[:200]
        except:
            body = "(unable to read)"
        print(f"Status: {status}")
        print(f"Body: {body}")
    except Exception as e:
        print(f"ERROR: {e}", file=sys.stderr)

def main():
    print("Starting probe_auth.py")
    print(f"Gateway: {GATEWAY}")
    
    # Mint token
    print("\n[Minting token...]")
    token = mint_token()
    if not token:
        print("ERROR: Failed to mint token", file=sys.stderr)
        sys.exit(1)
    print(f"Token acquired: {token[:30]}...")
    
    # Test 1: Health ZSC with valid key
    probe("T1: GET /api/v1/health/zsc/status with valid key", 
          "/api/v1/health/zsc/status", key=KEY)
    
    # Test 2: Health ZLS with valid key
    probe("T2: GET /api/v1/health/zls/status with valid key",
          "/api/v1/health/zls/status", key=KEY)
    
    # Test 3: Health ZSC with wrong key
    probe("T3: GET /api/v1/health/zsc/status with wrong key",
          "/api/v1/health/zsc/status", key=WRONG_KEY)
    
    # Test 4: Health ZSC with no credentials
    probe("T4: GET /api/v1/health/zsc/status with no credentials",
          "/api/v1/health/zsc/status")
    
    # Test 5: Health ZSC with bearer only
    probe("T5: GET /api/v1/health/zsc/status with bearer token only",
          "/api/v1/health/zsc/status", bearer=token)
    
    # Test 6: Health ZLS with bearer only
    probe("T6: GET /api/v1/health/zls/status with bearer token only",
          "/api/v1/health/zls/status", bearer=token)
    
    # Test 7: Devices with key alone
    probe("T7: GET /api/v1/devices/dev-0001/status with key alone",
          "/api/v1/devices/dev-0001/status", key=KEY)
    
    # Test 8: Devices with bearer
    probe("T8: GET /api/v1/devices/dev-0001/status with bearer token",
          "/api/v1/devices/dev-0001/status", bearer=token)
    
    # Test 9: Healthz anonymous
    probe("T9: GET /healthz with no credentials",
          "/healthz")
    
    print("\n=== Probe complete ===")

if __name__ == "__main__":
    main()
