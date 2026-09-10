#!/usr/bin/env bash
# Run end-to-end tests with the ZSC_E2E flag set.
# Requires all five services to be running; start them with scripts/run-all.sh first.
set -uo pipefail
cd "$(dirname "$0")/.."

ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo
