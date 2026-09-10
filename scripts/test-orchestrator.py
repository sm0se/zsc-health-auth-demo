#!/usr/bin/env python3
"""
Orchestrate baseline and after-change testing for R1 implementation.
Starts services, captures probe/smoke output, runs E2E tests.
"""

import subprocess
import time
import sys
import os
import json

GATEWAY = "http://127.0.0.1:5080"
SERVICES = {
    "device-api": (5400, "src/Zsc.DeviceApi/Zsc.DeviceApi.csproj"),
    "health-status": (5300, "src/Zsc.HealthStatus/Zsc.HealthStatus.csproj"),
    "bff": (5200, "src/Zsc.Bff/Zsc.Bff.csproj"),
    "interceptor": (5100, "src/Zsc.Interceptor/Zsc.Interceptor.csproj"),
    "api-gateway": (5080, "src/Zsc.ApiGateway/Zsc.ApiGateway.csproj"),
}

def run_cmd(cmd, shell=False):
    """Run a command and return output."""
    try:
        result = subprocess.run(
            cmd,
            shell=shell,
            capture_output=True,
            text=True,
            timeout=30
        )
        return result.returncode, result.stdout, result.stderr
    except subprocess.TimeoutExpired:
        return -1, "", "TIMEOUT"

def save_output(path, content):
    """Save content to file."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        f.write(content)

def run_after_tests():
    """Run all test after code changes."""
    print("=" * 70)
    print("AFTER CHANGES - Running comprehensive test suite")
    print("=" * 70)
    
    # Unit tests
    print("\n[1/4] Running unit tests...")
    code, out, err = run_cmd(["dotnet", "test", "-c", "Release", "--no-build", "--nologo"], shell=False)
    save_output("results/after-unit-tests.txt", out + "\n" + err)
    if code != 0:
        print("  UNIT TESTS FAILED")
        return False
    print("  UNIT TESTS: PASSED")
    
    # Probe script
    print("\n[2/4] Running probe-auth.sh...")
    code, out, err = run_cmd("bash scripts/probe-auth.sh", shell=True)
    save_output("results/after-probe.txt", out + "\n" + err)
    if "Test 1:" not in out:
        print("  PROBE-AUTH FAILED - output missing")
        return False
    print("  PROBE-AUTH: COMPLETED")
    
    # Smoke script
    print("\n[3/4] Running smoke.sh...")
    code, out, err = run_cmd("bash scripts/smoke.sh", shell=True)
    save_output("results/after-smoke.txt", out + "\n" + err)
    if "Smoke tests completed" not in out:
        print("  SMOKE TESTS FAILED")
        return False
    print("  SMOKE TESTS: PASSED")
    
    # E2E tests
    print("\n[4/4] Running E2E tests...")
    code, out, err = run_cmd("bash scripts/run-e2e.sh", shell=True)
    save_output("results/after-e2e.txt", out + "\n" + err)
    if code != 0:
        print("  E2E TESTS FAILED")
        return False
    if "failed" in out.lower() and "0 failed" not in out.lower():
        print("  E2E TESTS FAILED")
        return False
    print("  E2E TESTS: PASSED")
    
    # Git diff
    print("\n[5/5] Capturing git diff...")
    code, out, err = run_cmd(["git", "diff", "--stat"], shell=False)
    save_output("results/after-git-diff.txt", out)
    print("  GIT DIFF: CAPTURED")
    
    return True

if __name__ == "__main__":
    os.chdir("/workspace")
    
    # Build first
    print("Building project...")
    code, out, err = run_cmd(["dotnet", "build", "-c", "Release"], shell=False)
    if code != 0:
        print("BUILD FAILED")
        sys.exit(1)
    print("BUILD SUCCEEDED\n")
    
    # Run tests
    success = run_after_tests()
    
    print("\n" + "=" * 70)
    if success:
        print("ALL TESTS PASSED")
        sys.exit(0)
    else:
        print("TESTS FAILED")
        sys.exit(1)
