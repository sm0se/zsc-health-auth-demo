#!/usr/bin/env bash
# End-to-end test of the running chain. Requires the five services started by
# scripts/run-all.sh (or individually) and listening on their own ports.
ZSC_E2E=1 dotnet test tests/Zsc.E2E.Tests --nologo
