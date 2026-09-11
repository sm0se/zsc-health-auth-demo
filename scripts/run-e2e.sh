#!/usr/bin/env bash
# Runs the end-to-end suite against the five services that must already be
# running (scripts/run-all.sh, or the agent's `run` tool). Skipped entirely
# unless ZSC_E2E=1.
set -uo pipefail
cd "$(dirname "$0")/.."

ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo
