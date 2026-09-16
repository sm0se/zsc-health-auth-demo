#!/usr/bin/env bash
# End-to-end suite against the running chain. Start the five services first
# (scripts/run-all.sh, or the agent's `run` tool under kenCode).
set -euo pipefail
cd "$(dirname "$0")/.."

ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo
