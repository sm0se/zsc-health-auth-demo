#!/usr/bin/env bash
# Runs the end-to-end suite against the real running chain. Start the five
# services first (scripts/run-all.sh, or the agent's `run` tool).
set -uo pipefail
cd "$(dirname "$0")/.."

ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo
