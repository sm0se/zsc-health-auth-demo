#!/bin/bash
# Run end-to-end tests with ZSC_E2E=1 to enable E2EFact/E2ETheory tests
set -e
cd "$(dirname "$0")/.."
export ZSC_E2E=1
dotnet test tests/Zsc.E2E.Tests --nologo --verbosity normal
